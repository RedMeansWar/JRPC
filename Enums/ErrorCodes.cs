using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JRPC.Enums
{
    public enum ErrorCodes
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Incorrect function.
        /// </summary>
        InvalidFunction = 1,

        /// <summary>
        /// The system cannot find the file specified.
        /// </summary>
        FileNotFound = 2,

        /// <summary>
        /// The system cannot find the path specified.
        /// </summary>
        PathNotFound = 3,

        /// <summary>
        /// The system cannot open the file.
        /// </summary>
        TooManyOpenFiles = 4,

        /// <summary>
        /// Access is denied.
        /// </summary>
        AccessDenied = 5,

        /// <summary>
        /// The handle is invalid.
        /// </summary>
        InvalidHandle = 6,

        /// <summary>
        /// The storage control blocks were destroyed.
        /// </summary>
        ArenaTrashed = 7,

        /// <summary>
        /// Not enough storage is available to process this command.
        /// </summary>
        NotEnoughMemory = 8,

        /// <summary>
        /// The storage control block address is invalid.
        /// </summary>
        InvalidBlock = 9,

        /// <summary>
        /// The environment is incorrect.
        /// </summary>
        BadEnvironment = 10,

        /// <summary>
        /// An attempt was made to load a program with an incorrect format.
        /// </summary>
        BadFormat = 11,

        /// <summary>
        /// The access code is invalid.
        /// </summary>
        InvalidAccess = 12,

        /// <summary>
        /// The data is invalid.
        /// </summary>
        InvalidData = 13,

        /// <summary>
        /// Not enough storage is available to complete this operation.
        /// </summary>
        OutOfMemory = 14,

        /// <summary>
        /// The system cannot find the drive specified.
        /// </summary>
        InvalidDrive = 15,

        /// <summary>
        /// The directory cannot be removed.
        /// </summary>
        CurrentDirectory = 16,

        /// <summary>
        /// The system cannot move the file to a different disk drive.
        /// </summary>
        NotSameDevice = 17,

        /// <summary>
        /// There are no more files.
        /// </summary>
        NoMoreFiles = 18,

        /// <summary>
        /// The media is write-protected.
        /// </summary>
        WriteProtect = 19,

        /// <summary>
        /// The system cannot find the device specified.
        /// </summary>
        BadUnit = 20,

        /// <summary>
        /// The device is not ready.
        /// </summary>
        NotReady = 21,

        /// <summary>
        /// The device does not recognize the command.
        /// </summary>
        BadCommand = 22,

        /// <summary>
        /// Data error (cyclic redundancy check).
        /// </summary>
        Crc = 23,

        /// <summary>
        /// The program issued a command, but the command length is incorrect.
        /// </summary>
        BadLength = 24,
    }
}