using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSim.App.Services;


// Our message framing. TCP delivers a stream of bytes with no borders between messages,
// so every message travels as a frame: 4 bytes holding the length, then the bytes themselves.
// This class only moves bytes; what the bytes mean (and their encryption) is SecureChannel's job.
// The client and the server each have a copy of this file, and the two must stay identical
public static class MessageFraming
{
    // The length header is always exactly 4 bytes (one int)
    private const int HeaderSize = 4;

    // Refuse anything bigger than about 1 MB. Without a limit, a hostile client could write
    // "2 GB" in the header and make us allocate that much memory
    private const int MaxFrameSize = 1024 * 1024 + 1024;

    public static async Task SendFrameAsync(Socket socket, byte[] body, CancellationToken stop = default)
    {
        // One array for the whole frame: the header first, the body right after it
        byte[] frame = new byte[HeaderSize + body.Length];

        // Write the length into the first 4 bytes. Big-endian = most significant byte first,
        // the order network protocols use
        BinaryPrimitives.WriteInt32BigEndian(frame, body.Length);

        // Copy the body into the frame, starting right after the header
        body.CopyTo(frame, HeaderSize);

        // Send may accept only part of the bytes, so keep going until all of them are sent
        int sent = 0;
        while (sent < frame.Length)
        {
            sent += await socket.SendAsync(frame.AsMemory(sent), stop);
        }
    }

    // Returns the body of the next frame, or null if the other side closed the connection
    public static async Task<byte[]?> ReceiveFrameAsync(Socket socket, CancellationToken stop = default)
    {
        // Step 1: read exactly 4 bytes - the header
        byte[] header = new byte[HeaderSize];
        if (!await ReadExactAsync(socket, header, stop))
        {
            return null;
        }

        // Step 2: turn the 4 bytes back into a number - the length of the body
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 0 || length > MaxFrameSize)
        {
            throw new InvalidDataException($"Illegal frame length: {length}");
        }

        // Step 3: read exactly that many bytes - the body
        byte[] body = new byte[length];
        if (!await ReadExactAsync(socket, body, stop))
        {
            throw new IOException("The connection was closed in the middle of a frame.");
        }

        return body;
    }

    // Fills the whole buffer, however many Receive calls that takes.
    // Returns false if the other side closed the connection before the buffer was full
    private static async Task<bool> ReadExactAsync(Socket socket, byte[] buffer, CancellationToken stop)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            // Receive into the part of the buffer that is still empty
            int count = await socket.ReceiveAsync(buffer.AsMemory(total), stop);
            if (count == 0)
            {
                return false;
            }

            total += count;
        }

        return true;
    }
}
