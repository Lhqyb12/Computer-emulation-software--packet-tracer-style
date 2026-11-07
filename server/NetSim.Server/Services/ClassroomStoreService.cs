using System.Security.Cryptography;   // RandomNumberGenerator - secure random numbers for the join code
using Microsoft.EntityFrameworkCore;  // AsNoTracking, Include, FirstOrDefaultAsync, AnyAsync, ToListAsync, CountAsync
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Dtos;             // ClassroomDto, ClassroomMemberDto, ClassroomResponse, ClassroomListResponse
using NetSim.Server.Models;           // ClassroomRecord, ClassroomMemberRecord

namespace NetSim.Server.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The rules of classes, on the server, with the database: who may create, join, leave, remove and delete.
// These rules used to run in the client (ClassroomService). Here they run where nobody can skip them:
// every method gets the caller's user id from the verified token (MessageRouter reads it), never from
// the message's data. The join code is made here too, because only the server sees every class
public class ClassroomStoreService
{
    // The longest class name allowed - the same limit as the client's ClassroomService
    private const int MaxNameLength = 60;

    // How many characters a join code has
    private const int JoinCodeLength = 6;

    // The characters of a join code. 0/O and 1/I/L are left out on purpose: they look alike,
    // and a student copying the code from the board would type the wrong one
    private const string JoinCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly AppDbContext _db;          // the connection to the database
    private readonly SecurityLog _security;     // records security events (a refused attempt, a removed student)

    // Purpose: Builds the classroom store.
    // Input:   db - the database; security - the security event log (both passed in by dependency injection).
    // Output:  none (a constructor).
    public ClassroomStoreService(AppDbContext db, SecurityLog security)
    {
        _db = db;               // keep the database connection
        _security = security;   // keep the security log
    }

    // Purpose: Reads both halves of the Classes screen for one user.
    // Input:   userId - the signed-in user's id, from the verified token.
    // Output:  a ClassroomListResponse with the classes they teach and the classes they joined.
    public async Task<ClassroomListResponse> GetForUserAsync(int userId)
    {
        // The classes this user teaches - with everything: the code and every student
        var teaching = await _db.Classrooms
            .AsNoTracking()                                   // read-only: EF Core doesn't need to watch these rows
            .Where(c => c.TeacherUserId == userId)            // only classes this user teaches
            .OrderByDescending(c => c.CreatedAtUtc)           // newest first
            .Select(c => new ClassroomDto                     // each row -> a ClassroomDto
            {
                Id = c.Id,                                    // the id
                Name = c.Name,                                // the name
                JoinCode = c.JoinCode,                        // the code - the teacher may see it
                TeacherEmail = c.Teacher!.Email,              // the teacher's email, read through the navigation property
                CreatedAtUtc = c.CreatedAtUtc,                // when it was created
                StudentCount = c.Members.Count,               // how many member rows the class has
                Students = c.Members                          // every student of the class:
                    .OrderBy(m => m.JoinedAtUtc)              //   the first to join first
                    .Select(m => new ClassroomMemberDto       //   each member row -> a ClassroomMemberDto
                    {
                        Email = m.User!.Email,                //   the student's email
                        JoinedAtUtc = m.JoinedAtUtc           //   when they joined
                    })
                    .ToList()                                 //   collect into a list
            })
            .ToListAsync();                                   // run the query and collect the classes into a list

        // The classes this user joined - read through their own member rows
        var joinedRows = await _db.ClassroomMembers
            .AsNoTracking()                                   // read-only
            .Where(m => m.UserId == userId)                   // only this user's member rows
            .OrderByDescending(m => m.Classroom!.CreatedAtUtc) // newest class first
            .Select(m => new                                  // a small nameless object with what is needed:
            {
                m.ClassroomId,                                //   the class id
                m.Classroom!.Name,                            //   the class name
                TeacherEmail = m.Classroom.Teacher!.Email,    //   the teacher's email
                m.Classroom.CreatedAtUtc,                     //   when the class was created
                StudentCount = m.Classroom.Members.Count,     //   how many students it has
                m.User!.Email,                                //   this user's own email
                m.JoinedAtUtc                                 //   when this user joined
            })
            .ToListAsync();                                   // run the query

        // Build the DTOs in memory: no join code, and only the user's own row in Students
        var joined = joinedRows.Select(r => new ClassroomDto
        {
            Id = r.ClassroomId,                               // the class id
            Name = r.Name,                                    // the class name
            TeacherEmail = r.TeacherEmail,                    // who teaches it
            CreatedAtUtc = r.CreatedAtUtc,                    // when it was created
            StudentCount = r.StudentCount,                    // how many students it has
            Students = new List<ClassroomMemberDto>           // only me - other students' emails stay private
            {
                new ClassroomMemberDto { Email = r.Email, JoinedAtUtc = r.JoinedAtUtc }
            }
        }).ToList();                                          // collect into a list

        // Success, with both lists
        return new ClassroomListResponse { Success = true, Teaching = teaching, Joined = joined };
    }

    // Purpose: Creates a class taught by the signed-in user, with a new unique join code.
    // Input:   userId and email - the caller, from the verified token; name - the class name they typed.
    // Output:  a ClassroomResponse: Success + the new class (with its code), or Failure with the reason.
    public async Task<ClassroomResponse> CreateAsync(int userId, string? email, string? name)
    {
        // Empty name, or only spaces. The client checked this too - checked again, the server trusts no client
        if (string.IsNullOrWhiteSpace(name))
            return Fail("Class name cannot be empty.");

        // The name without spaces at both ends
        var trimmed = name.Trim();

        // Longer than 60 characters
        if (trimmed.Length > MaxNameLength)
            return Fail($"Class name cannot exceed {MaxNameLength} characters.");

        // The new row: a new id, the name, a fresh unique code, the caller as teacher, created now
        var classroom = new ClassroomRecord
        {
            Id = Guid.NewGuid(),                        // a new random id
            Name = trimmed,                             // the cleaned name
            JoinCode = await NewUniqueJoinCodeAsync(),  // a code no other class uses
            TeacherUserId = userId,                     // the teacher = the caller, from the token
            CreatedAtUtc = DateTime.UtcNow              // now, in UTC
        };

        // Mark it as new, and run the INSERT in the database
        _db.Classrooms.Add(classroom);
        await _db.SaveChangesAsync();

        // Success, with the class as the teacher sees it (the code, and no students yet)
        return new ClassroomResponse
        {
            Success = true,
            Classroom = new ClassroomDto
            {
                Id = classroom.Id,                          // the id
                Name = classroom.Name,                      // the name
                JoinCode = classroom.JoinCode,              // the code to give the students
                TeacherEmail = email ?? string.Empty,       // the caller's email ("??" = empty text if it is null)
                CreatedAtUtc = classroom.CreatedAtUtc       // when it was created
            }
        };
    }

    // Purpose: Adds the signed-in user as a student to the class that has this code.
    // Input:   userId and email - the caller, from the verified token; joinCode - the code they typed.
    // Output:  a ClassroomResponse: Success + the class (as a student sees it), or Failure with the reason.
    public async Task<ClassroomResponse> JoinAsync(int userId, string? email, string? joinCode)
    {
        // Small letters and spaces around the code are fine. "??" = if joinCode is null, use an empty text
        var code = (joinCode ?? string.Empty).Trim().ToUpperInvariant();

        // Check the format before searching: exactly 6 characters, all from the alphabet
        if (code.Length != JoinCodeLength || !code.All(ch => JoinCodeAlphabet.Contains(ch)))
            return Fail($"A join code is {JoinCodeLength} letters and digits.");

        // Find the class with this code, together with its teacher (Include = also load the Teacher row)
        var classroom = await _db.Classrooms
            .Include(c => c.Teacher)
            .FirstOrDefaultAsync(c => c.JoinCode == code);

        // No class has this code
        if (classroom is null)
            return Fail("No class has this code. Check it with your teacher.");

        // A teacher can't be a student in their own class
        if (classroom.TeacherUserId == userId)
            return Fail("You are the teacher of this class.");

        // Is there already a member row for this class and this user?
        bool alreadyIn = await _db.ClassroomMembers
            .AnyAsync(m => m.ClassroomId == classroom.Id && m.UserId == userId);

        // Already joined before
        if (alreadyIn)
            return Fail($"You already joined '{classroom.Name}'.");

        // The new member row: this class, this user, joined now
        var member = new ClassroomMemberRecord
        {
            ClassroomId = classroom.Id,     // the class
            UserId = userId,                // the student = the caller, from the token
            JoinedAtUtc = DateTime.UtcNow   // now, in UTC
        };

        // Mark it as new, and run the INSERT
        _db.ClassroomMembers.Add(member);
        await _db.SaveChangesAsync();

        // How many students the class has now (counted in the database)
        int count = await _db.ClassroomMembers.CountAsync(m => m.ClassroomId == classroom.Id);

        // Success, with the class as a student sees it: no join code, only their own row
        return new ClassroomResponse
        {
            Success = true,
            Classroom = new ClassroomDto
            {
                Id = classroom.Id,                                  // the id
                Name = classroom.Name,                              // the name
                TeacherEmail = classroom.Teacher?.Email ?? string.Empty,   // who teaches it
                CreatedAtUtc = classroom.CreatedAtUtc,              // when it was created
                StudentCount = count,                               // how many students it has now
                Students = new List<ClassroomMemberDto>             // only me
                {
                    new ClassroomMemberDto { Email = email ?? string.Empty, JoinedAtUtc = member.JoinedAtUtc }
                }
            }
        };
    }

    // Purpose: Removes the signed-in user from a class they joined.
    // Input:   userId - the caller, from the verified token; classroomId - the class to leave.
    // Output:  a ClassroomResponse: Success, or Failure with the reason.
    public async Task<ClassroomResponse> LeaveAsync(int userId, Guid classroomId)
    {
        // Find this user's own member row in this class. A user can only ever remove their own row here
        var member = await _db.ClassroomMembers
            .FirstOrDefaultAsync(m => m.ClassroomId == classroomId && m.UserId == userId);

        // No such row: the class doesn't exist, or the user is not in it
        if (member is null)
            return Fail("You are not a student in this class.");

        // Mark the row for deletion, and run the DELETE
        _db.ClassroomMembers.Remove(member);
        await _db.SaveChangesAsync();

        // Success, nothing to return
        return new ClassroomResponse { Success = true };
    }

    // Purpose: The teacher removes one student from their class.
    // Input:   userId and email - the caller, from the verified token; classroomId - the class;
    //          studentEmail - the student to remove.
    // Output:  a ClassroomResponse: Success, or Failure with the reason.
    public async Task<ClassroomResponse> RemoveStudentAsync(int userId, string? email, Guid classroomId, string? studentEmail)
    {
        // Find the class
        var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.Id == classroomId);

        // No such class
        if (classroom is null)
            return Fail("Class not found.");

        // Only the teacher of THIS class may remove its students. Anyone else: refuse, and record the attempt
        if (classroom.TeacherUserId != userId)
        {
            await _security.RecordAsync("Classroom access denied", email,
                $"Tried to remove a student from class {classroomId}, which they don't teach");
            return Fail("Only the teacher of this class can remove students.");
        }

        // The student's email in small letters, for a comparison that ignores upper/lower case
        var lowerEmail = (studentEmail ?? string.Empty).Trim().ToLower();

        // Find the member row of this class whose user has this email
        var member = await _db.ClassroomMembers
            .Include(m => m.User)                                                   // also load the student's User row
            .FirstOrDefaultAsync(m => m.ClassroomId == classroomId && m.User!.Email.ToLower() == lowerEmail);

        // No such student in this class
        if (member is null)
            return Fail("This student is not in the class.");

        // Mark the row for deletion, and run the DELETE
        _db.ClassroomMembers.Remove(member);
        await _db.SaveChangesAsync();

        // Record who was removed, and by whom - the student will just find the class gone
        await _security.RecordAsync("Student removed", member.User?.Email,
            $"Removed from class '{classroom.Name}' by its teacher {email}");

        // Success, nothing to return
        return new ClassroomResponse { Success = true };
    }

    // Purpose: The teacher deletes their whole class (its member rows are deleted with it - Cascade).
    // Input:   userId and email - the caller, from the verified token; classroomId - the class.
    // Output:  a ClassroomResponse: Success, or Failure with the reason.
    public async Task<ClassroomResponse> DeleteAsync(int userId, string? email, Guid classroomId)
    {
        // Find the class
        var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.Id == classroomId);

        // No such class
        if (classroom is null)
            return Fail("Class not found.");

        // Only the teacher of this class may delete it. Anyone else: refuse, and record the attempt
        if (classroom.TeacherUserId != userId)
        {
            await _security.RecordAsync("Classroom access denied", email,
                $"Tried to delete class {classroomId}, which they don't teach");
            return Fail("Only the teacher of this class can delete it.");
        }

        // Mark the class for deletion, and run the DELETE. The database deletes its member rows too (Cascade)
        _db.Classrooms.Remove(classroom);
        await _db.SaveChangesAsync();

        // Success, nothing to return
        return new ClassroomResponse { Success = true };
    }

    // Purpose: Makes a random join code that no class uses yet.
    // Input:   nothing.
    // Output:  a Task with a 6-character code, e.g. "K7QMTX".
    // RandomNumberGenerator (and not new Random()) because the code is the only thing that lets someone
    // into a class - it must not be predictable
    private async Task<string> NewUniqueJoinCodeAsync()
    {
        // Repeat until a free code is found (the return below leaves the loop)
        while (true)
        {
            // An empty array of 6 characters
            var chars = new char[JoinCodeLength];

            // For each of the 6 places: a secure random number 0..30 picks one character of the alphabet
            for (int i = 0; i < chars.Length; i++)
                chars[i] = JoinCodeAlphabet[RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length)];

            // The 6 characters as one text
            var code = new string(chars);

            // No class uses this code yet? Then it is ours. (Almost always true on the first try)
            if (!await _db.Classrooms.AnyAsync(c => c.JoinCode == code))
                return code;
        }
    }

    // Purpose: Builds a "refused" answer.
    // Input:   message - why it was refused.
    // Output:  a ClassroomResponse with Success = false and that message.
    private static ClassroomResponse Fail(string message) => new() { Success = false, Message = message };
}
