using System;                  // Basic types, and AsMemory() that we use on arrays
using System.Buffers.Binary;   // BinaryPrimitives: turns an int into 4 bytes and back
using System.IO;               // IOException and InvalidDataException
using System.Net.Sockets;      // The Socket class
using System.Threading;        // CancellationToken
using System.Threading.Tasks;  // Task, async/await

// The "folder" this class belongs to. Other files reach it with "using NetSim.Server.Networking"
namespace NetSim.Server.Networking;

// Our message framing. TCP delivers a stream of bytes with no borders between messages,
// so every message travels as a frame: 4 bytes holding the length, then the bytes themselves.
// This class only moves bytes; what the bytes mean (and their encryption) is SecureChannel's job.
// The client and the server each have a copy of this file, and the two must stay identical.
// "static" = there is no object to create, the methods are called directly on the class name
public static class MessageFraming
{
    // The length header is always exactly 4 bytes (one int).
    // "const" = a fixed value that can never change while the program runs
    private const int HeaderSize = 4;

    // The biggest body we agree to receive: 1 MB (1024 * 1024 bytes) plus 1 KB of spare room.
    // Without a limit, a hostile client could write "2 GB" in the header and make us
    // allocate that much memory
    private const int MaxFrameSize = 1024 * 1024 + 1024;

    // Sends one frame: the 4-byte length header and then the body.
    // socket = the connection to send on, body = the bytes of the message,
    // stop = a signal that cancels the send when the program shuts down ("= default" makes it optional).
    // "async Task" = the method can pause at an "await" without blocking a thread
    public static async Task SendFrameAsync(Socket socket, byte[] body, CancellationToken stop = default)
    {
        // One array for the whole frame: the header first, the body right after it
        byte[] frame = new byte[HeaderSize + body.Length];

        // Write the length into the first 4 bytes. Big-endian = most significant byte first,
        // the order network protocols use
        BinaryPrimitives.WriteInt32BigEndian(frame, body.Length);

        // Copy the body into the frame, starting right after the header (at index 4)
        body.CopyTo(frame, HeaderSize);

        // How many bytes of the frame were sent so far
        int sent = 0;

        // Send may accept only part of the bytes, so keep going until all of them are sent
        while (sent < frame.Length)
        {
            // AsMemory(sent) = the part of the frame from index "sent" to the end (what is still unsent).
            // SendAsync returns how many bytes it really sent, and we add that to the counter
            sent += await socket.SendAsync(frame.AsMemory(sent), stop);
        }
    }

    // Reads one frame and returns its body, or null if the other side closed the connection.
    // "byte[]?" = the result is a byte array or null
    public static async Task<byte[]?> ReceiveFrameAsync(Socket socket, CancellationToken stop = default)
    {
        // Step 1: read exactly 4 bytes - the header.
        // An empty 4-byte array that ReadExactAsync is going to fill
        byte[] header = new byte[HeaderSize];

        // Fill the header. false = the connection was closed before a new frame started
        if (!await ReadExactAsync(socket, header, stop))
        {
            // No more messages are coming. This is a normal ending, not an error
            return null;
        }

        // Step 2: turn the 4 bytes back into a number - the length of the body that follows
        int length = BinaryPrimitives.ReadInt32BigEndian(header);

        // A negative length is impossible, and one above the limit is refused
        if (length < 0 || length > MaxFrameSize)
        {
            // Stop here with an error; the caller catches it and closes the connection
            throw new InvalidDataException($"Illegal frame length: {length}");
        }

        // Step 3: read exactly that many bytes - the body.
        // An empty array in exactly the size the header announced
        byte[] body = new byte[length];

        // Fill the body. false = the connection was closed before the whole body arrived
        if (!await ReadExactAsync(socket, body, stop))
        {
            // Unlike in step 1 this IS an error: a header promised a body and only part of it came
            throw new IOException("The connection was closed in the middle of a frame.");
        }

        // The whole body arrived - hand it to the caller
        return body;
    }

    // Fills the whole buffer, however many Receive calls that takes.
    // Returns false if the other side closed the connection before the buffer was full.
    // "private" = only the methods of this class can call it
    private static async Task<bool> ReadExactAsync(Socket socket, byte[] buffer, CancellationToken stop)
    {
        // How many bytes of the buffer are filled so far
        int total = 0;

        // Keep reading until the buffer is full. One Receive may bring only part of what we asked for
        while (total < buffer.Length)
        {
            // Receive into the part of the buffer that is still empty (from index "total" to the end).
            // count = how many bytes really arrived in this call
            int count = await socket.ReceiveAsync(buffer.AsMemory(total), stop);

            // In TCP, receiving 0 bytes means the other side closed the connection
            if (count == 0)
            {
                // The buffer was not filled
                return false;
            }

            // Move the counter forward by what arrived
            total += count;
        }

        // The buffer is completely full
        return true;
    }
}
