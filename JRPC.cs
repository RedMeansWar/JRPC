using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using XDevkit;

namespace JRPC_Client
{
    #pragma warning disable
    public static class JRPC
    {
        #region Variables
        private static uint connectionId;
        
        private static readonly uint
            Void = 0u,
            Int = 1u,
            String = 2u,
            Float = 3u,
            Byte = 4u,
            IntArray = 5u,
            FloatArray = 6u,
            ByteArray = 7u,
            Uint64 = 8u,
            Uint64Array = 9u,
            JRPCVersion = 2u;

        private static Dictionary<Type, int> ValueTypeSizeMap = new()
        {
            { typeof(bool), 4 },
            { typeof(byte), 1 },
            { typeof(short), 2 },
            { typeof(int), 4 },
            { typeof(long), 8 },
            { typeof(ushort), 2 },
            { typeof(uint), 4 },
            { typeof(ulong), 8 },
            { typeof(float), 4 },
            { typeof(double), 8 }
        };

        private static Dictionary<Type, int> StructPrimitiveSizeMap = new();

        private static HashSet<Type> ValidReturnTypes = new()
        {
            typeof(void),
            typeof(bool),
            typeof(byte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(string),
            typeof(bool[]),
            typeof(byte[]),
            typeof(short[]),
            typeof(int[]),
            typeof(long[]),
            typeof(ushort[]),
            typeof(uint[]),
            typeof(ulong[]),
            typeof(float[]),
            typeof(double[]),
            typeof(string[])
        };

        private static byte[] 
            SMCMessage = new byte[16],
            SMCReturn = new byte[16];
        #endregion

        #region Checks
        /// <summary>
        /// Checks if the connection to the Xbox console is active.
        /// </summary>
        /// <param name="console">The IXboxConsole instance to check the connection status for.</param>
        /// <returns>True if the console is connected; otherwise, false.</returns>
        public static bool IsConnected(this IXboxConsole console)
        {
            if (console.Connect(out console)) return true;
            return false; // Return false if not connected to the console.
        }
        #endregion

        #region Connections
        /// <summary>
        /// Attempts to connect to an Xbox console. If no console name or IP is specified, defaults to a predefined console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Console">The connected Xbox console instance.</param>
        /// <param name="XboxNameOrIp">The name or IP address of the Xbox console. Defaults to "default".</param>
        /// <returns>True if the connection is successful, otherwise false.</returns>
        public static bool Connect(this IXboxConsole console, out IXboxConsole Console, string XboxNameOrIp = "default")
        {
            IXboxConsole Con;

            // Use default console name or IP if not specified
            if (XboxNameOrIp == "default")
            {
                XboxNameOrIp = new XboxManager().DefaultConsole;
            }

            // Open a connection to the specified Xbox console
            Con = new XboxManager().OpenConsole(XboxNameOrIp);

            int retry = 0; // Retry counter
            bool Connected = false; // Connection status

            // Attempt to connect to the console
            while (!Connected)
            {
                try
                {
                    // Open the connection
                    connectionId = Con.OpenConnection(null);
                    Connected = true; // Connection successful
                }
                catch (COMException ex)
                {
                    // Handle specific COM exceptions
                    if (ex.ErrorCode == UIntToInt(0x82DA0100) || ex.ErrorCode == UIntToInt(0x82DA0001) || ex.ErrorCode == UIntToInt(0x82DA0108))
                    {
                        // Retry up to three times if certain errors occur
                        if (retry >= 3)
                        {
                            Console = Con;
                            return false; // Connection failed after retries
                        }
                        retry++;
                        Delay(100); // Wait before retrying
                    }
                    else
                    {
                        // For other errors, return false immediately
                        Console = Con;
                        return false;
                    }
                }
            }

            // Return the connected console and status
            Console = Con;
            return true;
        }

        /// <summary>
        /// Sends a "bye" command to the Xbox console to gracefully disconnect.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        public static void Disconnect(this IXboxConsole console)
        {
            // Sends a "bye" command to the console to disconnect.
            SendCommand(console, "bye");
        }

        /// <summary>
        /// Reconnects to the Xbox console. If the console is currently connected, it will first disconnect and then attempt to reconnect.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Message">The message of the XNotify will display.</param>
        public static async void Reconnect(this IXboxConsole console, string Message = "Reconnected to console.")
        {
            try
            {
                // Disconnect the current connection.
                console.Disconnect();

                // Wait for 100 milliseconds before attempting to reconnect.
                Delay(100);

                // Attempt to reconnect to the console.
                console.Connect(out console);

                // Send a XNotify message stating that the connection was reenstablished
                console.XNotify(Message);
            }
            catch
            {
                throw new Exception("Couldn't reconnect to console. Was the console connected or was there another reason it failed?");
            }
        }
        #endregion

        #region LED's
        /// <summary>
        /// Sets the state of the Xbox console's LEDs.
        /// </summary>
        /// <param name="console">The IXboxConsole instance representing the connected Xbox console.</param>
        /// <param name="topLeftLED">The state of the top-left LED.</param>
        /// <param name="topRightLED">The state of the top-right LED.</param>
        /// <param name="bottomLeftLED">The state of the bottom-left LED.</param>
        /// <param name="bottomRightLED">The state of the bottom-right LED.</param>
        public static void SetLeds(this IXboxConsole console, LEDState Top_Left, LEDState Top_Right, LEDState Bottom_Left, LEDState Bottom_Right)
        {
            string command = "consolefeatures ver=" + JRPCVersion + " type=14 params=\"A\\0\\A\\4\\" + Int 
                + "\\" + (uint)Top_Left + "\\" + Int + "\\" + (uint)Top_Right + "\\" + Int + "\\" 
                + (uint)Bottom_Left + "\\" + Int + "\\" + (uint)Bottom_Right + "\\\"";
            SendCommand(console, command);
        }
        #endregion

        #region Calls And Command Sending
        /// <summary>
        /// Sends a command to the Xbox console and processes the response.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Command">The command string to send to the console.</param>
        /// <returns>The response from the console as a string.</returns>
        /// <exception cref="Exception">Thrown when an error occurs or JRPC is not installed on the console.</exception>
        public static string SendCommand(this IXboxConsole console, string Command)
        {
            string response;
            if (connectionId == null)
            {
                throw new Exception("IXboxConsole argument did not connect using JRPC's connect function.");
            }

            try
            {
                // Send the command to the console and capture the response
                console.SendTextCommand(connectionId, Command, out response);

                // Check if the response contains an error message
                if (response.Contains("error="))
                {
                    throw new Exception(response.Substring(11)); // Extract the error message from the response
                }

                // Check if the response indicates that JRPC is not installed
                if (response.Contains("DEBUG"))
                {
                    throw new Exception("JRPC is not installed on the current console");
                }
            }
            catch (COMException ex)
            {
                // Handle specific COM exception where JRPC is not installed
                if (ex.ErrorCode == UIntToInt(0x82DA0007))
                {
                    throw new Exception("JRPC is not installed on the current console");
                }
                else
                {
                    throw ex; // Re-throw any other COM exceptions
                }
            }

            // Return the console's response if no error were encountered
            return response;
        }

        /// <summary>
        /// Calls a function on the Xbox console at the specified address with the provided arguments.
        /// </summary>
        /// <typeparam name="T">The return type of the function call.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address of the function to be called.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>Returns the result of the function call as type T.</returns>
        public static T Call<T>(this IXboxConsole console, uint Address, params object[] Arguments) where T : struct
        {
            // Calls the specified function at the provided address with the arguments and casts the result to T.
            return (T)CallArgs(console, SystemThread: true, TypeToType<T>(Array: false), typeof(T), null, 0, Address, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module name and ordinal with the provided arguments.
        /// </summary>
        /// <typeparam name="T">The return type of the function call.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="module">The module name containing the function to be called.</param>
        /// <param name="ordinal">The ordinal number of the function to be called within the module.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>Returns the result of the function call as type T.</returns>
        public static T Call<T>(this IXboxConsole console, string module, int ordinal, params object[] Arguments) where T : struct
        {
            // Calls the function in the specified module using its ordinal, and casts the result to T.
            return (T)CallArgs(console, SystemThread: true, TypeToType<T>(Array: false), typeof(T), module, ordinal, 0u, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by address and thread type with the provided arguments.
        /// </summary>
        /// <typeparam name="T">The return type of the function call.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="Address">The address of the function to be called.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>Returns the result of the function call as type T.</returns>
        public static T Call<T>(this IXboxConsole console, ThreadType Type, uint Address, params object[] Arguments) where T : struct
        {
            // Calls the function at the specified address using the specified thread type and casts the result to T.
            return (T)CallArgs(console, Type == ThreadType.System, TypeToType<T>(Array: false), typeof(T), null, 0, Address, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module, ordinal, and thread type with the provided arguments.
        /// </summary>
        /// <typeparam name="T">The return type of the function call.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="module">The module name containing the function.</param>
        /// <param name="ordinal">The ordinal number of the function in the module.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>Returns the result of the function call as type T.</returns>
        public static T Call<T>(this IXboxConsole console, ThreadType Type, string module, int ordinal, params object[] Arguments) where T : struct
        {
            // Calls the function in the specified module and ordinal using the specified thread type and casts the result to T.
            return (T)CallArgs(console, Type == ThreadType.System, TypeToType<T>(Array: false), typeof(T), module, ordinal, 0u, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by address and returns no value.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The address of the function to call.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        public static void CallVoid(this IXboxConsole console, uint Address, params object[] Arguments)
        {
            CallArgs(console, SystemThread: true, Void, typeof(void), null, 0, Address, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module and ordinal, returning no value.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="module">The module name containing the function.</param>
        /// <param name="ordinal">The ordinal number of the function in the module.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        public static void CallVoid(this IXboxConsole console, string module, int ordinal, params object[] Arguments)
        {
            CallArgs(console, SystemThread: true, Void, typeof(void), module, ordinal, 0u, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by address with the specified thread type, returning no value.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="Address">The address of the function to call.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        public static void CallVoid(this IXboxConsole console, ThreadType Type, uint Address, params object[] Arguments)
        {
            CallArgs(console, Type == ThreadType.System, Void, typeof(void), null, 0, Address, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module, ordinal, and thread type, returning no value.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="module">The module name containing the function.</param>
        /// <param name="ordinal">The ordinal number of the function in the module.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        public static void CallVoid(this IXboxConsole console, ThreadType Type, string module, int ordinal, params object[] Arguments)
        {
            CallArgs(console, Type == ThreadType.System, Void, typeof(void), module, ordinal, 0u, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by address and returns an array of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of elements in the returned array.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The address of the function to call.</param>
        /// <param name="ArraySize">The size of the array to be returned.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>An array of the specified type.</returns>
        public static T[] CallArray<T>(this IXboxConsole console, uint Address, uint ArraySize, params object[] Arguments) where T : struct
        {
            if (ArraySize == 0)
            {
                return new T[1];
            }

            return (T[])CallArgs(console, SystemThread: true, TypeToType<T>(Array: true), typeof(T), null, 0, Address, ArraySize, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module and ordinal, returning an array of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of elements in the returned array.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="module">The module name containing the function.</param>
        /// <param name="ordinal">The ordinal number of the function in the module.</param>
        /// <param name="ArraySize">The size of the array to be returned.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>An array of the specified type.</returns>
        public static T[] CallArray<T>(this IXboxConsole console, string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct
        {
            if (ArraySize == 0)
            {
                return new T[1];
            }

            return (T[])CallArgs(console, SystemThread: true, TypeToType<T>(Array: true), typeof(T), module, ordinal, 0u, ArraySize, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by address with the specified thread type, returning an array of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of elements in the returned array.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="Address">The address of the function to call.</param>
        /// <param name="ArraySize">The size of the array to be returned.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>An array of the specified type.</returns>
        public static T[] CallArray<T>(this IXboxConsole console, ThreadType Type, uint Address, uint ArraySize, params object[] Arguments) where T : struct
        {
            if (ArraySize == 0)
            {
                return new T[1];
            }

            return (T[])CallArgs(console, Type == ThreadType.System, TypeToType<T>(Array: true), typeof(T), null, 0, Address, ArraySize, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module, ordinal, and thread type, returning an array of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of elements in the returned array.</typeparam>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="module">The module name containing the function.</param>
        /// <param name="ordinal">The ordinal number of the function in the module.</param>
        /// <param name="ArraySize">The size of the array to be returned.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>An array of the specified type.</returns>
        public static T[] CallArray<T>(this IXboxConsole console, ThreadType Type, string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct
        {
            if (ArraySize == 0)
            {
                return new T[1];
            }

            return (T[])CallArgs(console, Type == ThreadType.System, TypeToType<T>(Array: true), typeof(T), module, ordinal, 0u, ArraySize, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by address and returns the result as a string.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The address of the function to call.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>The result of the function call as a string.</returns>
        public static string CallString(this IXboxConsole console, uint Address, params object[] Arguments)
        {
            return (string)CallArgs(console, SystemThread: true, String, typeof(string), null, 0, Address, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module and ordinal, returning the result as a string.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="module">The module name containing the function.</param>
        /// <param name="ordinal">The ordinal number of the function in the module.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>The result of the function call as a string.</returns>
        public static string CallString(this IXboxConsole console, string module, int ordinal, params object[] Arguments)
        {
            return (string)CallArgs(console, SystemThread: true, String, typeof(string), module, ordinal, 0u, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by address with the specified thread type, returning the result as a string.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="Address">The address of the function to call.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>The result of the function call as a string.</returns>
        public static string CallString(this IXboxConsole console, ThreadType Type, uint Address, params object[] Arguments)
        {
            return (string)CallArgs(console, Type == ThreadType.System, String, typeof(string), null, 0, Address, 0u, Arguments);
        }

        /// <summary>
        /// Calls a function on the Xbox console by module, ordinal, and thread type, returning the result as a string.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Type">The thread type to use for the function call (System or User thread).</param>
        /// <param name="module">The module name containing the function.</param>
        /// <param name="ordinal">The ordinal number of the function in the module.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>The result of the function call as a string.</returns>
        public static string CallString(this IXboxConsole console, ThreadType Type, string module, int ordinal, params object[] Arguments)
        {
            return (string)CallArgs(console, Type == ThreadType.System, String, typeof(string), module, ordinal, 0u, 0u, Arguments);
        }

        /// <summary>
        /// Launches an XEX file on the console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Path">The XEX file path. Example: Hdd:\somedir\somexex.xex</param>
        /// <param name="Directory">The XEX file location. Example: Hdd:\somedir\</param>
        /// <returns>True if the console was able to launch the XEX file, otherwise false.</returns>
        public static bool LaunchXex(this IXboxConsole console, string Path, string Directory)
        {
            string XEX = "\"" + Path + "\""; // Add "" around the path, to prevent errors with paths including white spaces
            string DIR = "\"" + Directory + "\""; // Add "" around the path, to prevent errors with paths including white spaces

            string resp = console.SendCommand("magicboot Title=" + XEX + " Directory=" + DIR + "\r\n"); // Concatenate parameters
            if (!(resp.Contains("202")) || resp.Contains("203")) // Check if it worked
            {
                return false; // Return false if it didn't work
            }
            return true; // Return true if it did
        }
        #endregion

        #region Notify
        /// <summary>
        /// Sends an Xbox notification with a custom message and type.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Message">The message to display in the notification.</param>
        /// <param name="Type">The type of notification to display (as an integer).</param>
        public static void XNotify(this IXboxConsole console, string Message, int Type)
        {
            string command = "consolefeatures ver=" + JRPCVersion + " type=12 params=\"A\\0\\A\\2\\" 
                + JRPC.String + "/" + Message.Length + "\\" 
                + Message.ToHexString() + "\\" 
                + JRPC.Int + "\\" 
                + Type + "\\\"";

            SendCommand(console, command);
        }

        /// <summary>
        /// Sends an Xbox notification with a custom message and a default or specified notification type.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Message">The message to display in the notification.</param>
        /// <param name="Type">The type of notification to display (default is FlashingXboxConsole).</param>
        public static void XNotify(this IXboxConsole console, string Message, XNotifyType Type = XNotifyType.FlashingXboxConsole) => XNotify(console, Message, (int)Type);
        #endregion

        #region Console Information
        /// <summary>
        /// Retrieves the IP address of the Xbox console and returns it as a string.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The IP address of the console as a string.</returns>
        public static string XboxIP(this IXboxConsole console)
        {
            // Convert the IP address from byte array to a string representation
            byte[] bytes = BitConverter.GetBytes(console.IPAddress);
            Array.Reverse(bytes);
            return new IPAddress(bytes).ToString();
        }

        /// <summary>
        /// Retrieves the CPU key of the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The CPU key of the console as a string.</returns>
        public static string GetCPUKey(this IXboxConsole console)
        {
            // Construct the command to get the CPU key
            string command = "consolefeatures ver=" + JRPCVersion + " type=10 params=\"A\\0\\A\\0\\\"";
            // Send the command and retrieve the response
            string text = SendCommand(console, command);
            // Extract and return the CPU key from the response
            return text.Substring(text.Find(" ") + 1);
        }

        /// <summary>
        /// Retrieves the kernel version of the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The kernel version of the console as a uint.</returns>
        public static uint GetKernalVersion(this IXboxConsole console)
        {
            // Construct the command to get the kernel version
            string command = "consolefeatures ver=" + JRPCVersion + " type=13 params=\"A\\0\\A\\0\\\"";
            // Send the command and retrieve the response
            string text = SendCommand(console, command);
            // Parse and return the kernel version from the response
            return uint.Parse(text.Substring(text.Find(" ") + 1));
        }

        /// <summary>
        /// Retrieves the temperature of a specified component in the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="TemperatureType">The type of temperature to retrieve.</param>
        /// <returns>The temperature of the specified component as a uint.</returns>
        public static uint GetTemperature(this IXboxConsole console, TemperatureType TemperatureType)
        {
            // Construct the command to get the temperature
            string command = "consolefeatures ver=" + JRPCVersion + " type=15 params=\"A\\0\\A\\1\\" + Int + "\\" + (int)TemperatureType + "\\\"";
            // Send the command and retrieve the response
            string text = SendCommand(console, command);
            // Extract and return the temperature from the response
            return uint.Parse(text.Substring(text.Find(" ") + 1), NumberStyles.HexNumber);
        }

        /// <summary>
        /// Retrieves the type of the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The type of the console as a string.</returns>
        public static string ConsoleType(this IXboxConsole console)
        {
            // Construct the command to get the console type
            string command = "consolefeatures ver=" + JRPCVersion + " type=17 params=\"A\\0\\A\\0\\\"";
            // Send the command and retrieve the response
            string text = SendCommand(console, command);
            // Extract and return the console type from the response
            return text.Substring(text.Find(" ") + 1);
        }

        /// <summary>
        /// Retrieves the current title ID from the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The current title ID as a uint.</returns>
        public static uint XamGetCurrentTitleId(this IXboxConsole console)
        {
            // Construct the command to get the current title ID
            string command = "consolefeatures ver=" + JRPCVersion + " type=16 params=\"A\\0\\A\\0\\\"";
            // Send the command and retrieve the response
            string text = SendCommand(console, command);
            // Extract and return the title ID from the response
            return uint.Parse(text.Substring(text.Find(" ") + 1), NumberStyles.HexNumber);
        }

        /// <summary>
        /// Gets the name of the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The name of the console.</returns>
        public static string GetName(this IXboxConsole console)
        {
            string consoleName = "";

            // If the console is not connected return the string as empty
            if (!console.IsConnected()) return "";

            // Try to get the console name, if not catch the error
            try { consoleName = console.Name; }
            catch {}

            return consoleName; // Return the console name
        }

        /// <summary>
        /// Gets the XUID of the console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The console's id.</returns>
        public static string GetXUID(this IXboxConsole console)
        {
            string xuid = "";

            // If the console is not connection return a string as empty
            if (!console.IsConnected()) return "";

            // Try to get the console XUID, if not catch the error
            try { xuid = console.SendCommand("xuid").Replace("200- ", string.Empty); }
            catch { }

            return xuid; // Return the console's XUID
        }

        /// <summary>
        /// Get's the Debug Monitor version number.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <returns>The debug monitor version.</returns>
        public static string GetDMVersion(this IXboxConsole console)
        {
            string debugMonitor = "";

            // If the console is not connection return a string as empty
            if (!console.IsConnected()) return "";

            // Try to get the console debug monitor version, if not catch the error
            try { debugMonitor = console.SendCommand("dmversion").Replace("200- ", string.Empty); }
            catch { }

            return debugMonitor; // Return the debug monitor version
        }
        #endregion

        #region Console Features
        /// <summary>
        /// Freeze or unfreeze the console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Freeze">Freeze or unfreeze the console.</param>
        public static void FreezeConsole(this IXboxConsole console, bool Freeze)
        {
            if (Freeze)
            {
                // If the freeze boolean is true, freeze the console
                console.SendCommand("stop");
            }

            // Unfreeze the console if false
            console.SendCommand("go");
        }

        /// <summary>
        /// Sets the DVD tray to open or closed.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Open">Toggle if the disk tray is open or closed.</param>
        public static void SetTrayState(this IXboxConsole console, TrayState State)
        {
            switch (State)
            {
                case TrayState.Open:
                    console.CallVoid(console.ResolveFunction("xam.xex", (int)XboxShortcuts.OpenTray), [0, 0, 0, 0]);
                    break;

                case TrayState.Close:
                    console.CallVoid(console.ResolveFunction("xam.xex", (int)XboxShortcuts.CloseTray), [0, 0, 0, 0]);
                    break;
            }
        }

        /// <summary>
        /// Sends a shutdown command to the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        public static void ShutDownConsole(this IXboxConsole console)
        {
            try
            {
                // Construct the shutdown command for the console.
                string command = "consolefeatures ver=" + JRPCVersion + " type=11 params=\"A\\0\\A\\0\\\"";

                // Sends the shutdown command to the console.
                SendCommand(console, command);
            }
            catch
            {
                // Suppress any exceptions to avoid crashing if the shutdown command fails.
            }
        }

        /// <summary>
        /// Restarts the console.
        /// </summary>
        /// <param name="console">This instance of the IXboxConsole interface.</param>
        /// <param name="RebootFlag">Restart type.</param>
        public static void Reboot(this IXboxConsole console, RebootFlag RebootFlag) => console.Reboot(null, null, null, (XboxRebootFlags)RebootFlag);

        /// <summary>
        /// Sets the console's fan speed to a desired amount.
        /// </summary>
        /// <param name="console"></param>
        /// <param name="fan">The fan ID (1 is usually the default.)</param>
        /// <param name="speed">The fan speed.</param>
        /// <returns>Returns true if the fan speed has been modified, otherwise false.</returns>
        public static bool FanSpeed(this IXboxConsole console, int fan, int speed)
        {
            uint uint0 = console.ResolveFunction("xboxkrnl.exe", 0x29);
            byte[] byte0 = new byte[0x10];
            byte[] byte1 = new byte[0x10];

            Array.Clear(byte0, 0, byte0.Length);
            Array.Clear(byte1 , 0, byte1.Length);

            if (fan == 1)
            {
                byte0[0] = 0x94;
            }
            else
            {
                if (fan != 2)
                {
                    return false;
                }

                byte0[0] = 0x89;
            }

            if (speed > 100)
            {
                speed = 100;
            }

            if (speed <= 0)
            {
                speed = 50;
            }

            if (speed < 0x2d)
            {
                byte0[1] = 0x7f;
            }
            else
            {
                byte0[1] = (byte)(speed | 0x80);
            }

            object[] args = new object[2];
            console.CallVoid(uint0, args);
            return true;
        }

        /// <summary>
        /// Sets the console's fan speed to a desired amount.
        /// </summary>
        /// <param name="console"></param>
        /// <param name="fan">The fan ID (1 is usually the default.)</param>
        /// <param name="speed">The fan speed.</param>
        /// <returns>Returns true if the fan speed has been modified, otherwise false.</returns>
        public static bool SetFanSpeed(this IXboxConsole console, int fan, int speed) => console.FanSpeed(fan, speed);
        #endregion

        #region Profiles
        /// <summary>
        /// Grabs the console's assigned default profile.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="XUID">The XUID of the console.</param>
        public static void GetUserDefaultProfile(this IXboxConsole console, out long XUID)
        {
            console.SendCommand("autoprof");
            XUID = 0;
        }

        /// <summary>
        /// Set's the console's default profile.
        /// </summary>
        /// <param name="console">The instance of IXboxConsole interface.</param>
        /// <param name="XUID">The XUID of the console.</param>
        public static void SetUserDefaultProfile(this IXboxConsole console, long XUID) => console.SendCommand("autoprof xuid=" + XUID);

        public static void GetSignInState(this IXboxConsole console) => console.ResolveFunction("xboxkrnl.exe", 528);

        public static void QuickSignIn(this IXboxConsole console) => console.ResolveFunction("xam.xex", 700);
        #endregion

        #region Shortcuts
        /// <summary>
        /// Quickly launches a Xbox shortcut.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="UI">The user-interface of the shortcut to go to.</param>
        public static void XboxShortcut(this IXboxConsole console, XboxShortcuts UI)
        {
            switch (UI)
            {
                case XboxShortcuts.XboxHome:
                    console.Reboot(XboxDirectory.XboxHome, XboxDirectory.XboxHome, XboxDirectory.XboxHome, XboxRebootFlags.Title);
                    break;

                case XboxShortcuts.AvatarEditor:
                    console.Reboot(XboxDirectory.AvatarEditor, XboxDirectory.AvatarEditor, XboxDirectory.AvatarEditor, XboxRebootFlags.Title);
                    break;

                case XboxShortcuts.DriveSelector:
                    console.Reboot(XboxDirectory.DriveSelector, XboxDirectory.DriveSelector, XboxDirectory.DriveSelector, XboxRebootFlags.Title);
                    break;

                case XboxShortcuts.TurnOffConsole:
                    console.ShutDownConsole();
                    break;

                case XboxShortcuts.AccountManagement:
                    console.CallVoid(console.ResolveFunction("xam.xex", (int)XboxShortcuts.AccountManagement));
                    break;

                case XboxShortcuts.Achievements:
                    console.CallVoid(console.ResolveFunction("xam.xex", (int)XboxShortcuts.Achievements));
                    break;

                case XboxShortcuts.ActiveDownloads:
                    console.CallVoid(console.ResolveFunction("xam.xex", (int)XboxShortcuts.ActiveDownloads));
                    break;

                case XboxShortcuts.Awards:
                    console.CallVoid(console.ResolveFunction("xam.xex", (int)XboxShortcuts.Awards));
                    break;

                case XboxShortcuts.BeaconsAndActiviy:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.BeaconsAndActiviy));
                    break;

                case XboxShortcuts.FamilySettings:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.FamilySettings));
                    break;

                case XboxShortcuts.Friends:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.Friends));
                    break;

                case XboxShortcuts.GuideButton:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.GuideButton));
                    break;

                case XboxShortcuts.Messages:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.Messages), 0);
                    break;

                case XboxShortcuts.MyGames:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.MyGames), new object[] { 0, 0, 0, 0 });
                    break;

                case XboxShortcuts.OpenTray:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.OpenTray), new object[] { 0, 0, 0, 0 });
                    break;

                case XboxShortcuts.CloseTray:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.CloseTray));
                    break;

                case XboxShortcuts.Party:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.Party), new object[] { 0, 0, 0, 0 });
                    break;

                case XboxShortcuts.Profile:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.Profile));
                    break;

                case XboxShortcuts.Recent:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.Recent));
                    break;

                case XboxShortcuts.RedeemCode:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.Recent));
                    break;

                case XboxShortcuts.SelectMusic:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.SelectMusic));
                    break;

                case XboxShortcuts.SystemMusicPlayer:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.SystemMusicPlayer));
                    break;

                case XboxShortcuts.SystemSettings:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.SystemSettings));
                    break;

                case XboxShortcuts.WindowsMediaCenter:
                    console.CallVoid(console.ResolveFunction("xam.xex", (uint)XboxShortcuts.WindowsMediaCenter));
                    break;
            }
        }

        /// <summary>
        /// Sets the color of the console in Xbox 360 Neighborhood.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Color">The color of the console to display.</param>
        public static void SetConsoleColor(this IXboxConsole console, XboxColor Color) => console.SendCommand("setcolor name=" + Enum.GetName(typeof(int), Color).ToLower());

        /// <summary>
        /// Changes the of the console in Xbox 360 Neighborhood.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Name">The name of the console to display.</param>
        public static void ChangeConsoleName(this IXboxConsole console, string Name) => console.SendTextCommand(0, "dbgname name=" + Name, out _);

        /// <summary>
        /// Gets all of the folders in a certain directory.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole</param>
        /// <param name="Directory"></param>
        /// <returns>A list of all the folders in the directory.</returns>
        public static string[] GetDirectories(this IXboxConsole console, string Directory)
        {
            IXboxFiles xboxFiles = console.DirectoryFiles(Directory);
            List<string> dirList = new();

            foreach (IXboxFile xboxFile in xboxFiles)
            {
                if (xboxFile.IsDirectory)
                {
                    dirList.Add(xboxFile.Name);
                }
            }

            return dirList.ToArray();
        }

        /// <summary>
        /// Gets all files in a certain directory.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Directory">The directory to get all the files from.</param>
        /// <returns>A list of all the files in the directory.</returns>
        public static string[] GetFiles(this IXboxConsole console, string Directory)
        {
            IXboxFiles xboxFiles = console.DirectoryFiles(Directory);
            List<string> fileList = new();

            foreach (IXboxFile file in xboxFiles)
            {
                fileList.Add(file.Name);
            }

            return fileList.ToArray();
        }
        #endregion

        #region Conversion
        /// <summary>
        /// Converts a string to its hexadecimal representation.
        /// </summary>
        /// <param name="String">The string to convert.</param>
        /// <returns>The hexadecimal representation of the string as a string.</returns>
        public static string ToHexString(this string String)
        {
            // Initialize an empty string to hold the hex representation
            string text = "";
            // Iterate over each character in the string
            for (int i = 0; i < String.Length; i++)
            {
                // Convert each character to its byte value and then to a hex string, appending it to the result
                text += ((byte)String[i]).ToString("X2");
            }

            return text;
        }

        /// <summary>
        /// Converts a string to a byte array.
        /// </summary>
        /// <param name="String">The string to convert.</param>
        /// <returns>The byte array representation of the string.</returns>
        public static byte[] ToByteArray(this string String)
        {
            // Create a byte array with a length of the string length plus one
            byte[] array = new byte[String.Length + 1];
            // Iterate over each character in the string
            for (int i = 0; i < String.Length; i++)
            {
                // Convert each character to its byte value and store it in the array
                array[i] = (byte)String[i];
            }

            return array;
        }
        #endregion

        #region Controller
        /// <summary>
        /// Sends a command to the Xbox console to start processing input for a specified user.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index whose input should be processed.</param>
        public static void GetInputProcess(this IXboxConsole console, UserIndex Index)
        {
            console.SendCommand("autoinput user=" + Index + " process");
        }

        /// <summary>
        /// Binds a controller to a specified user on the Xbox console with a given input queue length.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index to bind the controller to.</param>
        /// <param name="QueueLength">The length of the input queue to bind to the user.</param>
        public static void BindController(this IXboxConsole console, UserIndex Index, uint QueueLength)
        {
            console.SendCommand("autoinput user=" + Index + " bind queuelen=" + QueueLength);
        }

        /// <summary>
        /// Unbinds the controller from a specified user on the Xbox console, stopping any input processing for that user.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index to unbind the controller from.</param>
        public static void UnbindController(this IXboxConsole console, UserIndex Index)
        {
            console.SendCommand("autoinput user=" + Index + " unbind");
        }

        /// <summary>
        /// Sends a command to connect a controller for the specified user on the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index to connect the controller to.</param>
        public static void ConnectController(this IXboxConsole console, UserIndex Index)
        {
            console.SendCommand("autoinput user=" + Index + " connect");
        }

        /// <summary>
        /// Sends a command to disconnect the controller for the specified user on the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index to disconnect the controller from.</param>
        public static void DisconnectController(this IXboxConsole console, UserIndex Index)
        {
            console.SendCommand("autoinput user=" + Index + " disconnect");
        }

        /// <summary>
        /// Sends a command to set the gamepad state for the specified user on the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index to set the gamepad state for.</param>
        /// <param name="GamePad">The gamepad state to be set.</param>
        public static void SetGamePadState(this IXboxConsole console, UserIndex Index, ref XBOX_AUTOMATION_GAMEPAD GamePad)
        {
            console.SendCommand("autoinput user=" + Index + " setpacket");
        }

        /// <summary>
        /// Queues gamepad input packets for the specified user on the Xbox console, specifying the timed duration and packet count duration.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index for which the gamepad state will be queued.</param>
        /// <param name="Gamepad">The gamepad state to be queued.</param>
        /// <param name="TimedDuration">The timed duration for which the gamepad state will be queued.</param>
        /// <param name="CountDuration">The number of packets to queue.</param>
        /// <returns>Returns true if the command is sent successfully.</returns>
        public static bool QueueGamepadState(this IXboxConsole console, UserIndex Index, ref XBOX_AUTOMATION_GAMEPAD Gamepad, uint TimedDuration, uint CountDuration)
        {
            console.SendCommand("autoinput user=" + Index + " queuepackets count=" + CountDuration);
            return true;
        }

        /// <summary>
        /// Clears the gamepad input queue for the specified user on the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index for which the gamepad queue will be cleared.</param>
        public static void ClearGamePadQueue(this IXboxConsole console, UserIndex Index)
        {
            console.SendCommand("autoinput user=" + Index + " clearqueue");
        }

        /// <summary>
        /// Queries the current status of the gamepad input queue for the specified user on the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Index">The user index for which the gamepad queue status is queried.</param>
        /// <param name="QueueLength">Outputs the length of the gamepad queue.</param>
        /// <param name="ItemsInQueue">Outputs the number of items currently in the queue.</param>
        /// <param name="TimedDurationRemaining">Outputs the remaining time duration for queued packets.</param>
        /// <param name="CountDurationRemaining">Outputs the remaining count duration for queued packets.</param>
        public static void QueryGamepadQueue(this IXboxConsole console, UserIndex Index, out uint QueueLength, out uint ItemsInQueue, out uint TimedDurationRemaining, out uint CountDurationRemaining)
        {
            QueueLength = 0;
            ItemsInQueue = 0;
            TimedDurationRemaining = 0;
            CountDurationRemaining = 0;
            console.SendCommand("autoinput user=" + Index + " queryqueue");
        }
        #endregion

        #region Pushing
        /// <summary>
        /// Appends a byte value to the end of a byte array and returns the new array.
        /// </summary>
        /// <param name="InArray">The original byte array.</param>
        /// <param name="OutArray">The new byte array with the appended value.</param>
        /// <param name="Value">The byte value to append to the array.</param>
        public static void Push(this byte[] InArray, out byte[] OutArray, byte Value)
        {
            // Create a new byte array with a length of the original array plus one
            OutArray = new byte[InArray.Length + 1];
            // Copy the original array elements to the new array
            InArray.CopyTo(OutArray, 0);
            // Append the new byte value at the end of the new array
            OutArray[InArray.Length] = Value;
        }
        #endregion

        #region Finding And Reading Data
        /// <summary>
        /// Finds the first occurrence of a specified substring within the given string.
        /// </summary>
        /// <param name="String">The string in which to search for the substring.</param>
        /// <param name="_Ptr">The substring to search for within the string.</param>
        /// <returns>
        /// The zero-based index of the first occurrence of the substring if found; otherwise, -1.
        /// </returns>
        public static int Find(this string String, string _Ptr)
        {
            // Return -1 if either the main string or the substring is empty.
            if (_Ptr.Length == 0 || String.Length == 0)
            {
                return -1;
            }

            // Loop through the main string character by character.
            for (int i = 0; i < String.Length; i++)
            {
                // If the current character does not match the first character of the substring, continue.
                if (String[i] != _Ptr[0])
                {
                    continue;
                }

                bool flag = true;

                // Check if the rest of the substring matches.
                for (int j = 0; j < _Ptr.Length; j++)
                {
                    // If the substring exceeds the main string's length or characters do not match, set flag to false.
                    if (i + j >= String.Length || String[i + j] != _Ptr[j])
                    {
                        flag = false;
                        break;
                    }
                }

                // If a match is found, return the index.
                if (flag)
                {
                    return i;
                }
            }

            // Return -1 if no match is found.
            return -1;
        }

        /// <summary>
        /// Retrieves a block of memory from the Xbox console at the specified address and length.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <param name="Length">The number of bytes to read.</param>
        /// <returns>A byte array containing the retrieved memory.</returns>
        public static byte[] GetMemory(this IXboxConsole console, uint Address, uint Length)
        {
            uint BytesRead = 0u;
            // Create a byte array to hold the memory read from the console.
            byte[] array = new byte[Length];

            // Read the memory from the specified address into the array.
            console.DebugTarget.GetMemory(Address, Length, array, out BytesRead);

            // Invalidate the memory cache for executable pages, ensuring the cache is updated.
            console.DebugTarget.InvalidateMemoryCache(ExecutablePages: true, Address, Length);

            return array;
        }

        /// <summary>
        /// Reads a signed byte from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The signed byte read from memory.</returns>
        public static sbyte ReadSByte(this IXboxConsole console, uint Address)
        {
            return (sbyte)console.GetMemory(Address, 1u)[0];
        }

        /// <summary>
        /// Reads an unsigned byte from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The unsigned byte read from memory.</returns>
        public static byte ReadByte(this IXboxConsole console, uint Address)
        {
            return console.GetMemory(Address, 1u)[0];
        }

        /// <summary>
        /// Reads a boolean value from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The boolean value read from memory (true if the byte is non-zero, false otherwise).</returns>
        public static bool ReadBool(this IXboxConsole console, uint Address)
        {
            return console.GetMemory(Address, 1u)[0] != 0;
        }

        /// <summary>
        /// Reads a float value from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The float value read from memory.</returns>
        public static float ReadFloat(this IXboxConsole console, uint Address)
        {
            // Get 4 bytes of memory from the specified address.
            byte[] memory = console.GetMemory(Address, 4u);

            // Reverse the bytes if needed (for endianness differences).
            ReverseBytes(memory, 4);

            // Convert the byte array into a float and return it.
            return BitConverter.ToSingle(memory, 0);
        }

        /// <summary>
        /// Reads an array of 32-bit floats from the specified memory address in the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to start reading from.</param>
        /// <param name="ArraySize">The number of floats to read.</param>
        /// <returns>An array of 32-bit float values read from the memory.</returns>
        public static float[] ReadFloat(this IXboxConsole console, uint Address, uint ArraySize)
        {
            // Initialize an array to store the float values.
            float[] array = new float[ArraySize];

            // Get the memory from the specified address. Each float requires 4 bytes, so we read ArraySize * 4 bytes.
            byte[] memory = console.GetMemory(Address, ArraySize * 4);

            // Reverse the byte order for each float (if necessary for the platform's endianness).
            ReverseBytes(memory, 4);

            // Iterate through the memory and convert each 4-byte segment into a float.
            for (int i = 0; i < ArraySize; i++)
            {
                array[i] = BitConverter.ToSingle(memory, i * 4);
            }

            return array;
        }

        /// <summary>
        /// Reads a signed 16-bit integer from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The signed 16-bit integer read from memory.</returns>
        public static short ReadInt16(this IXboxConsole console, uint Address)
        {
            byte[] memory = console.GetMemory(Address, 2u);
            ReverseBytes(memory, 2);
            return BitConverter.ToInt16(memory, 0);
        }

        /// <summary>
        /// Reads an array of signed 16-bit integers from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to start reading from.</param>
        /// <param name="ArraySize">The number of 16-bit integers to read.</param>
        /// <returns>An array of signed 16-bit integers read from memory.</returns>
        public static short[] ReadInt16(this IXboxConsole console, uint Address, uint ArraySize)
        {
            // Initialize an array to store the short values.
            short[] array = new short[ArraySize];

            // Get the memory from the specified address. Each short requires 2 bytes, so we read ArraySize * 2 bytes.
            byte[] memory = console.GetMemory(Address, ArraySize * 2);

            // Reverse the byte order for each short (if necessary for the platform's endianness).
            ReverseBytes(memory, 2);

            // Iterate through the memory and convert each 2-byte segment into a short.
            for (int i = 0; i < ArraySize; i++)
            {
                array[i] = BitConverter.ToInt16(memory, i * 2);
            }

            return array;
        }

        /// <summary>
        /// Reads an unsigned 16-bit integer from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The unsigned 16-bit integer read from memory.</returns>
        public static ushort ReadUInt16(this IXboxConsole console, uint Address)
        {
            byte[] memory = console.GetMemory(Address, 2u);
            ReverseBytes(memory, 2);
            return BitConverter.ToUInt16(memory, 0);
        }

        /// <summary>
        /// Reads an array of unsigned 16-bit integers from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to start reading from.</param>
        /// <param name="ArraySize">The number of 16-bit integers to read.</param>
        /// <returns>An array of unsigned 16-bit integers read from memory.</returns>
        public static ushort[] ReadUInt16(this IXboxConsole console, uint Address, uint ArraySize)
        {
            // Initialize an array to store the UInt16 values
            ushort[] array = new ushort[ArraySize];

            // Read the memory at the specified address
            byte[] memory = console.GetMemory(Address, ArraySize * 2);

            // Reverse the byte order in blocks of 2 bytes for endianness
            ReverseBytes(memory, 2);

            // Convert each 2-byte block into a UInt16 and store it in the array
            for (int i = 0; i < ArraySize; i++)
            {
                array[i] = BitConverter.ToUInt16(memory, i * 2);
            }

            // Return the array of UInt16 values
            return array;
        }

        /// <summary>
        /// Reads a signed 32-bit integer from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The signed 32-bit integer read from memory.</returns>
        public static int ReadInt32(this IXboxConsole console, uint Address)
        {
            byte[] memory = console.GetMemory(Address, 4u);
            ReverseBytes(memory, 4);
            return BitConverter.ToInt32(memory, 0);
        }

        /// <summary>
        /// Reads an array of signed 32-bit integers from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to start reading from.</param>
        /// <param name="ArraySize">The number of 32-bit integers to read.</param>
        /// <returns>An array of signed 32-bit integers read from memory.</returns>
        public static int[] ReadInt32(this IXboxConsole console, uint Address, uint ArraySize)
        {
            // Initialize an array to hold the Int32 values to be read
            int[] array = new int[ArraySize];

            // Retrieve the bytes from memory at the specified address
            byte[] memory = console.GetMemory(Address, ArraySize * 4);

            // Reverse the byte order in blocks of 4 bytes to handle endianness
            ReverseBytes(memory, 4);

            // Convert each 4-byte block into an Int32 and store in the array
            for (int i = 0; i < ArraySize; i++)
            {
                array[i] = BitConverter.ToInt32(memory, i * 4);
            }

            // Return the array of Int32 values
            return array;
        }

        /// <summary>
        /// Reads an unsigned 32-bit integer from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The unsigned 32-bit integer read from memory.</returns>
        public static uint ReadUInt32(this IXboxConsole console, uint Address)
        {
            byte[] memory = console.GetMemory(Address, 4u);
            ReverseBytes(memory, 4);
            return BitConverter.ToUInt32(memory, 0);
        }

        /// <summary>
        /// Reads an array of unsigned 32-bit integers from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to start reading from.</param>
        /// <param name="ArraySize">The number of 32-bit integers to read.</param>
        /// <returns>An array of unsigned 32-bit integers read from memory.</returns>
        public static uint[] ReadUInt32(this IXboxConsole console, uint Address, uint ArraySize)
        {
            // Initialize an array to hold the UInt32 values to be read
            uint[] array = new uint[ArraySize];

            // Retrieve the bytes from memory at the specified address
            byte[] memory = console.GetMemory(Address, ArraySize * 4);

            // Reverse the byte order in blocks of 4 bytes to handle endianness
            ReverseBytes(memory, 4);

            // Convert each 4-byte block into a UInt32 and store in the array
            for (int i = 0; i < ArraySize; i++)
            {
                array[i] = BitConverter.ToUInt32(memory, i * 4);
            }

            // Return the array of UInt32 values
            return array;
        }

        /// <summary>
        /// Reads a signed 64-bit integer from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The signed 64-bit integer read from memory.</returns>
        public static long ReadInt64(this IXboxConsole console, uint Address)
        {
            // Retrieve 8 bytes of data from memory at the specified address
            byte[] memory = console.GetMemory(Address, 8u);

            // Reverse the byte order in blocks of 8 bytes to handle endianness
            ReverseBytes(memory, 8);

            // Convert the 8-byte block into a signed 64-bit integer and return it
            return BitConverter.ToInt64(memory, 0);
        }

        /// <summary>
        /// Reads an array of signed 64-bit integers from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to start reading from.</param>
        /// <param name="ArraySize">The number of 64-bit integers to read.</param>
        /// <returns>An array of signed 64-bit integers read from memory.</returns>
        public static long[] ReadInt64(this IXboxConsole console, uint Address, uint ArraySize)
        {
            // Initialize an array to hold the Int64 values to be read
            long[] array = new long[ArraySize];

            // Retrieve the bytes from memory at the specified address
            byte[] memory = console.GetMemory(Address, ArraySize * 8);

            // Reverse the byte order in blocks of 8 bytes to handle endianness
            ReverseBytes(memory, 8);

            // Convert each 8-byte block into an Int64 and store in the array
            for (int i = 0; i < ArraySize; i++)
            {
                array[i] = BitConverter.ToInt64(memory, i * 8);
            }

            // Return the array of Int64 values
            return array;
        }

        /// <summary>
        /// Reads an unsigned 64-bit integer from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <returns>The unsigned 64-bit integer read from memory.</returns>
        public static ulong ReadUInt64(this IXboxConsole console, uint Address)
        {
            byte[] memory = console.GetMemory(Address, 8u);
            ReverseBytes(memory, 8);
            return BitConverter.ToUInt64(memory, 0);
        }

        /// <summary>
        /// Reads an array of unsigned 64-bit integers from the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to start reading from.</param>
        /// <param name="ArraySize">The number of 64-bit integers to read.</param>
        /// <returns>An array of unsigned 64-bit integers read from memory.</returns>
        public static ulong[] ReadUInt64(this IXboxConsole console, uint Address, uint ArraySize)
        {
            // Initialize an array to hold the UInt64 values to be read
            ulong[] array = new ulong[ArraySize];

            // Retrieve the bytes from memory at the specified address
            byte[] memory = console.GetMemory(Address, ArraySize * 8);

            // Reverse the byte order in blocks of 8 bytes to handle endianness
            ReverseBytes(memory, 8);

            // Convert each 8-byte block into a UInt64 and store in the array
            for (int i = 0; i < ArraySize; i++)
            {
                array[i] = BitConverter.ToUInt64(memory, i * 8);
            }

            // Return the array of UInt64 values
            return array;
        }

        /// <summary>
        /// Reads a string from the Xbox console memory at a specified address, interpreting the memory as UTF-8 encoded.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to read from.</param>
        /// <param name="size">The number of bytes to read, which should correspond to the length of the string.</param>
        /// <returns>The string read from the memory.</returns>
        public static string ReadString(this IXboxConsole console, uint Address, uint size)
        {
            return Encoding.UTF8.GetString(console.GetMemory(Address, size));
        }
        #endregion

        #region Writing & Setting Memory
        /// <summary>
        /// Writes a byte array to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Data">The byte array to write to memory.</param>
        public static void SetMemory(this IXboxConsole console, uint Address, byte[] Data)
        {
            console.DebugTarget.SetMemory(Address, (uint)Data.Length, Data, out var _);
        }

        /// <summary>
        /// Writes a signed byte value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The signed byte value to write.</param>
        public static void WriteSByte(this IXboxConsole console, uint Address, sbyte Value)
        {
            console.SetMemory(Address, new byte[1] { BitConverter.GetBytes(Value)[0] });
        }

        /// <summary>
        /// Writes an array of signed 8-bit integers to the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of signed 8-bit integers to write.</param>
        public static void WriteSByte(this IXboxConsole console, uint Address, sbyte[] Value)
        {
            // Initialize a byte array to hold the converted values
            byte[] OutArray = new byte[0];

            // Convert each sbyte value to byte and add to the output array
            for (int i = 0; i < Value.Length; i++)
            {
                byte value = (byte)Value[i];
                OutArray.Push(out OutArray, value);
            }

            // Write the byte array to the specified memory address
            console.SetMemory(Address, OutArray);
        }

        /// <summary>
        /// Writes a byte value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The byte value to write.</param>
        public static void WriteByte(this IXboxConsole console, uint Address, byte Value)
        {
            console.SetMemory(Address, new byte[1] { Value });
        }

        /// <summary>
        /// Writes an array of byte values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of byte values to write.</param>
        public static void WriteByte(this IXboxConsole console, uint Address, byte[] Value)
        {
            console.SetMemory(Address, Value);
        }

        /// <summary>
        /// Writes a boolean value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The boolean value to write.</param>
        public static void WriteBool(this IXboxConsole console, uint Address, bool Value)
        {
            console.SetMemory(Address, new byte[1] { (byte)(Value ? 1u : 0u) });
        }

        /// <summary>
        /// Writes an array of boolean values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of boolean values to write.</param>
        public static void WriteBool(this IXboxConsole console, uint Address, bool[] Value)
        {
            byte[] OutArray = new byte[0];
            for (int i = 0; i < Value.Length; i++)
            {
                OutArray.Push(out OutArray, (byte)(Value[i] ? 1u : 0u));
            }

            console.SetMemory(Address, OutArray);
        }

        /// <summary>
        /// Writes a float value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The float value to write.</param>
        public static void WriteFloat(this IXboxConsole console, uint Address, float Value)
        {
            byte[] bytes = BitConverter.GetBytes(Value);
            Array.Reverse(bytes);
            console.SetMemory(Address, bytes);
        }

        /// <summary>
        /// Writes an array of single-precision floating-point numbers to the Xbox console at the specified address.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of single-precision floating-point numbers to write.</param>
        public static void WriteFloat(this IXboxConsole console, uint Address, float[] Value)
        {
            // Initialize a byte array to hold the floating-point values as bytes
            byte[] array = new byte[Value.Length * 4];

            // Convert each float value to bytes and copy to the output array
            for (int i = 0; i < Value.Length; i++)
            {
                BitConverter.GetBytes(Value[i]).CopyTo(array, i * 4);
            }

            // Reverse the byte order in blocks of 4 bytes to handle endianness
            ReverseBytes(array, 4);

            // Write the byte array to the specified memory address
            console.SetMemory(Address, array);
        }

        /// <summary>
        /// Writes a 16-bit signed integer value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The 16-bit signed integer value to write.</param>
        public static void WriteInt16(this IXboxConsole console, uint Address, short Value)
        {
            byte[] bytes = BitConverter.GetBytes(Value);
            ReverseBytes(bytes, 2);
            console.SetMemory(Address, bytes);
        }

        /// <summary>
        /// Writes an array of 16-bit signed integer values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of 16-bit signed integer values to write.</param>
        public static void WriteInt16(this IXboxConsole console, uint Address, short[] Value)
        {
            byte[] array = new byte[Value.Length * 2];
            for (int i = 0; i < Value.Length; i++)
            {
                BitConverter.GetBytes(Value[i]).CopyTo(array, i * 2);
            }

            ReverseBytes(array, 2);
            console.SetMemory(Address, array);
        }

        /// <summary>
        /// Writes a 16-bit unsigned integer value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The 16-bit unsigned integer value to write.</param>
        public static void WriteUInt16(this IXboxConsole console, uint Address, ushort Value)
        {
            byte[] bytes = BitConverter.GetBytes(Value);
            ReverseBytes(bytes, 2);
            console.SetMemory(Address, bytes);
        }

        /// <summary>
        /// Writes an array of 16-bit unsigned integer values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of 16-bit unsigned integer values to write.</param>
        public static void WriteUInt16(this IXboxConsole console, uint Address, ushort[] Value)
        {
            byte[] array = new byte[Value.Length * 2];
            for (int i = 0; i < Value.Length; i++)
            {
                BitConverter.GetBytes(Value[i]).CopyTo(array, i * 2);
            }

            ReverseBytes(array, 2);
            console.SetMemory(Address, array);
        }

        /// <summary>
        /// Writes a 32-bit signed integer value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The 32-bit signed integer value to write.</param>
        public static void WriteInt32(this IXboxConsole console, uint Address, int Value)
        {
            byte[] bytes = BitConverter.GetBytes(Value);
            ReverseBytes(bytes, 4);
            console.SetMemory(Address, bytes);
        }

        /// <summary>
        /// Writes an array of 32-bit signed integer values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of 32-bit signed integer values to write.</param>
        public static void WriteInt32(this IXboxConsole console, uint Address, int[] Value)
        {
            byte[] array = new byte[Value.Length * 4];
            for (int i = 0; i < Value.Length; i++)
            {
                BitConverter.GetBytes(Value[i]).CopyTo(array, i * 4);
            }

            ReverseBytes(array, 4);
            console.SetMemory(Address, array);
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The 32-bit unsigned integer value to write.</param>
        public static void WriteUInt32(this IXboxConsole console, uint Address, uint Value)
        {
            byte[] bytes = BitConverter.GetBytes(Value);
            ReverseBytes(bytes, 4);
            console.SetMemory(Address, bytes);
        }

        /// <summary>
        /// Writes an array of 32-bit unsigned integer values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of 32-bit unsigned integer values to write.</param>
        public static void WriteUInt32(this IXboxConsole console, uint Address, uint[] Value)
        {
            byte[] array = new byte[Value.Length * 4];
            for (int i = 0; i < Value.Length; i++)
            {
                BitConverter.GetBytes(Value[i]).CopyTo(array, i * 4);
            }

            ReverseBytes(array, 4);
            console.SetMemory(Address, array);
        }

        /// <summary>
        /// Writes a 64-bit signed integer value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The 64-bit signed integer value to write.</param>
        public static void WriteInt64(this IXboxConsole console, uint Address, long Value)
        {
            byte[] bytes = BitConverter.GetBytes(Value);
            ReverseBytes(bytes, 8);
            console.SetMemory(Address, bytes);
        }

        /// <summary>
        /// Writes an array of 64-bit signed integer values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of 64-bit signed integer values to write.</param>
        public static void WriteInt64(this IXboxConsole console, uint Address, long[] Value)
        {
            byte[] array = new byte[Value.Length * 8];
            for (int i = 0; i < Value.Length; i++)
            {
                BitConverter.GetBytes(Value[i]).CopyTo(array, i * 8);
            }

            ReverseBytes(array, 8);
            console.SetMemory(Address, array);
        }

        /// <summary>
        /// Writes a 64-bit unsigned integer value to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The 64-bit unsigned integer value to write.</param>
        public static void WriteUInt64(this IXboxConsole console, uint Address, ulong Value)
        {
            byte[] bytes = BitConverter.GetBytes(Value);
            ReverseBytes(bytes, 8);
            console.SetMemory(Address, bytes);
        }

        /// <summary>
        /// Writes an array of 64-bit unsigned integer values to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="Value">The array of 64-bit unsigned integer values to write.</param>
        public static void WriteUInt64(this IXboxConsole console, uint Address, ulong[] Value)
        {
            byte[] array = new byte[Value.Length * 8];
            for (int i = 0; i < Value.Length; i++)
            {
                BitConverter.GetBytes(Value[i]).CopyTo(array, i * 8);
            }

            ReverseBytes(array, 8);
            console.SetMemory(Address, array);
        }

        /// <summary>
        /// Writes a string to a specified memory address on the Xbox console.
        /// </summary>
        /// <param name="console">The Xbox console instance.</param>
        /// <param name="Address">The memory address to write to.</param>
        /// <param name="String">The string to write.</param>
        public static void WriteString(this IXboxConsole console, uint Address, string String)
        {
            byte[] OutArray = new byte[0];
            for (int i = 0; i < String.Length; i++)
            {
                byte value = (byte)String[i];
                OutArray.Push(out OutArray, value);
            }

            // Null-terminate the string
            OutArray.Push(out OutArray, 0);
            console.SetMemory(Address, OutArray);
        }
        #endregion

        #region Memory
        public static void DumpMemory(this IXboxConsole console, uint Length, string FileName)
        {
            byte[] packet = new byte[1026];

            // Send GetMemEx command
        }
        #endregion

        #region Resolve Function
        /// <summary>
        /// Resolves a function address from a specified module by its ordinal number on the Xbox console.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="ModuleName">The name of the module to resolve the function from.</param>
        /// <param name="Ordinal">The ordinal number of the function to resolve.</param>
        /// <returns>The address of the function resolved from the module.</returns>
        public static uint ResolveFunction(this IXboxConsole console, string ModuleName, uint Ordinal)
        {
            // Construct the command string to resolve the function address
            string command = "consolefeatures ver=" + JRPCVersion + " type=9 params=\"A\\0\\A\\2\\"
                + ModuleName.Length + "\\" + ModuleName.ToHexString() + "\\"
                + Ordinal + "\\\"";

            // Send the command to the console and get the response text
            string text = SendCommand(console, command);

            // Extract and parse the function address from the response text
            return uint.Parse(text.Substring(text.Find(" ") + 1), NumberStyles.HexNumber);
        }
        #endregion

        #region WCHAR
        /// <summary>
        /// Converts a string to a WCHAR (Wide Character) array.
        /// </summary>
        /// <param name="String">The string to convert.</param>
        /// <returns>A byte array representing the WCHAR encoding of the string.</returns>
        public static byte[] WCHAR(string String)
        {
            byte[] array = new byte[String.Length * 2 + 2]; // Allocate space for WCHAR representation and null terminator
            int num = 1; // Start at index 1 to leave space for null terminator
            for (int i = 0; i < String.Length; i++)
            {
                byte b = (byte)String[i]; // Get byte representation of the character
                array[num] = b; // Set WCHAR value
                num += 2; // Move to the next WCHAR position
            }

            return array;
        }

        /// <summary>
        /// Converts the current string to a WCHAR (Wide Character) array.
        /// </summary>
        /// <param name="String">The string to convert.</param>
        /// <returns>A byte array representing the WCHAR encoding of the string.</returns>
        public static byte[] ToWCHAR(this string String)
        {
            return WCHAR(String); // Calls the WCHAR method
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Converts an unsigned integer (uint) to a signed integer (int).
        /// </summary>
        /// <param name="value">The unsigned integer value to convert.</param>
        /// <returns>The converted signed integer value.</returns>
        private static int UIntToInt(uint value)
        {
            // Convert the uint value to a btye array.
            byte[] array = BitConverter.GetBytes(value);

            // Convert the byte array back to an int and return it.
            return BitConverter.ToInt32(array, 0);
        }

        /// <summary>
        /// Delays a task by a number of inputed miliseconds.
        /// </summary>
        /// <param name="milliseconds">The time in miliseconds to delay the task.</param>
        private static async void Delay(int milliseconds) => Thread.Sleep(milliseconds);

        /// <summary>
        /// Converts an array of integers to a byte array, with each integer occupying 4 bytes.
        /// </summary>
        /// <param name="iArray">The array of integers to convert.</param>
        /// <returns>A byte array representing the integer array.</returns>
        private static byte[] IntArrayToByte(int[] iArray)
        {
            byte[] array = new byte[iArray.Length * 4]; // Allocate space for 4 bytes per integer
            int num = 0; // Index for iterating through the integer array
            int num2 = 0; // Index for placing bytes in the byte array
            while (num < iArray.Length)
            {
                for (int i = 0; i < 4; i++)
                {
                    array[num2 + i] = BitConverter.GetBytes(iArray[num])[i]; // Convert each integer to bytes
                }

                num++;
                num2 += 4; // Move to the next position in the byte array
            }

            return array;
        }

        /// <summary>
        /// Converts an object to an unsigned 64-bit integer (ulong), based on its type.
        /// </summary>
        /// <param name="o">The object to convert.</param>
        /// <returns>An unsigned 64-bit integer representation of the object.</returns>
        private static ulong ConvertToUInt64(object o)
        {
            if (o is bool)
            {
                return (bool)o ? 1ul : 0ul; // Convert boolean to ulong
            }

            if (o is byte)
            {
                return (byte)o; // Convert byte to ulong
            }

            if (o is short)
            {
                return (ulong)(short)o; // Convert short to ulong
            }

            if (o is int)
            {
                return (ulong)(int)o; // Convert int to ulong
            }

            if (o is long)
            {
                return (ulong)(long)o; // Convert long to ulong
            }

            if (o is ushort)
            {
                return (ulong)(ushort)o; // Convert ushort to ulong
            }

            if (o is uint)
            {
                return (ulong)(uint)o; // Convert uint to ulong
            }

            if (o is ulong)
            {
                return (ulong)o; // No conversion needed for ulong
            }

            if (o is float)
            {
                return (ulong)BitConverter.DoubleToInt64Bits((float)o); // Convert float to ulong
            }

            if (o is double)
            {
                return (ulong)BitConverter.DoubleToInt64Bits((double)o); // Convert double to ulong
            }

            return 0; // Return 0 if the type is not supported
        }

        #region Booleans
        /// <summary>
        /// Checks if the provided type is a valid structure type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is a value type but not a primitive type; otherwise, false.</returns>
        private static bool IsValidStructType(Type type)
        {
            if (!type.IsPrimitive) // Check if the type is not a primitive
            {
                return type.IsValueType; // Return true if the type is a value type (e.g., struct)
            }

            return false; // Return false for primitive types
        }

        /// <summary>
        /// Checks if the provided type is a valid return type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is in the list of valid return types; otherwise, false.</returns>
        private static bool IsValidReturnType(Type type)
        {
            return ValidReturnTypes.Contains(type); // Check if the type is in the valid return types list
        }
        #endregion

        #region Reversing
        /// <summary>
        /// Reverses the bytes in the buffer in groups of a specified size.
        /// </summary>
        /// <param name="buffer">The byte array to reverse.</param>
        /// <param name="groupSize">The size of each group of bytes to reverse.</param>
        /// <exception cref="ArgumentException">Thrown when the buffer length is not a multiple of the group size.</exception>
        private static void ReverseBytes(byte[] buffer, int groupSize)
        {
            // Check if the buffer length is a multiple of the group size
            if (buffer.Length % groupSize != 0)
            {
                throw new ArgumentException("Group size must be a multiple of the buffer length", nameof(groupSize));
            }

            // Reverse bytes in each group of the specified size
            for (int i = 0; i < buffer.Length; i += groupSize)
            {
                int start = i;
                int end = i + groupSize - 1;

                // Swap bytes from start and end of the group
                while (start < end)
                {
                    byte temp = buffer[start];
                    buffer[start] = buffer[end];
                    buffer[end] = temp;
                    start++;
                    end--;
                }
            }
        }
        #endregion

        #region Types
        /// <summary>
        /// Maps a generic type to a specific type identifier based on whether it is an array or not.
        /// </summary>
        /// <typeparam name="T">The type to be mapped.</typeparam>
        /// <param name="Array">Indicates whether the type is an array or a single value.</param>
        /// <returns>A type identifier corresponding to the type and array status.</returns>
        private static uint TypeToType<T>(bool Array) where T : struct
        {
            Type typeFromHandle = typeof(T);

            // Map primitive numeric types to their respective identifiers
            if ((object)typeFromHandle == typeof(int) || (object)typeFromHandle == typeof(uint) ||
                (object)typeFromHandle == typeof(short) || (object)typeFromHandle == typeof(ushort))
            {
                if (Array)
                {
                    return IntArray;
                }

                return Int;
            }

            // Map string and char array types to their respective identifier
            if ((object)typeFromHandle == typeof(string) || (object)typeFromHandle == typeof(char[]))
            {
                return String;
            }

            // Map floating point types to their respective identifiers
            if ((object)typeFromHandle == typeof(float) || (object)typeFromHandle == typeof(double))
            {
                if (Array)
                {
                    return FloatArray;
                }

                return Float;
            }

            // Map byte and char types to their respective identifiers
            if ((object)typeFromHandle == typeof(byte) || (object)typeFromHandle == typeof(char))
            {
                if (Array)
                {
                    return ByteArray;
                }

                return Byte;
            }

            // Map unsigned long and long types to their respective identifiers
            if ((object)typeFromHandle == typeof(ulong) || (object)typeFromHandle == typeof(long))
            {
                if (Array)
                {
                    return Uint64Array;
                }

                return Uint64;
            }

            // Default case for unsupported types
            return Uint64;
        }
        #endregion

        #region Command Args
        /// <summary>
        /// Calls a function on the Xbox console with specified arguments and retrieves the result.
        /// </summary>
        /// <param name="console">The instance of the IXboxConsole interface.</param>
        /// <param name="SystemThread">Indicates whether to use the system thread.</param>
        /// <param name="Type">The type identifier for the function call.</param>
        /// <param name="t">The return type of the function.</param>
        /// <param name="module">The module name for the function call.</param>
        /// <param name="ordinal">The ordinal of the function within the module.</param>
        /// <param name="Address">The memory address for the function call.</param>
        /// <param name="ArraySize">The size of the array, if applicable.</param>
        /// <param name="Arguments">The arguments to pass to the function.</param>
        /// <returns>The result of the function call, converted to the appropriate type.</returns>
        private static object CallArgs(IXboxConsole console, bool SystemThread, uint Type, Type t, string module, int ordinal, uint Address, uint ArraySize, params object[] Arguments)
        {
            // Validate the return type is supported
            if (!IsValidReturnType(t))
            {
                throw new Exception(string.Concat(new object[4]
                {
                    "Invalid type ",
                    t.Name,
                    Environment.NewLine, 
                    "JRPC only supports: bool, byte, short, int, long, ushort, uint, ulong, float, double"
                }));
            }

            uint connectTimeout = (console.ConversationTimeout = 4000000u);
            console.ConnectTimeout = connectTimeout;

            // Construct the command string
            string text = "consolefeatures ver=" + JRPCVersion + " type=" + Type + (SystemThread ? " system" : "") + ((module != null) ? (" module=\"" + module + "\" ord=" + ordinal) : "") + " as=" + ArraySize + " params=\"A\\" + Address.ToString("X") + "\\A\\" + Arguments.Length + "\\";

            // Check for argument length limit
            if (Arguments.Length > 37)
            {
                throw new Exception("Cannot use more than 37 parameters in a call");
            }

            foreach (object obj in Arguments)
            {
                bool flag = false;

                // Handle uint arguments
                if (obj is uint)
                {
                    text = string.Concat(text, Int, "\\", UIntToInt((uint)obj), "\\");
                    flag = true;
                }
                // Handle int, bool, and byte arguments
                if (obj is int || obj is bool || obj is byte)
                {
                    if (obj is bool)
                    {
                        text = string.Concat(text, Int, "/", Convert.ToInt32((bool)obj), "\\");
                    }
                    else
                    {
                        text = string.Concat(text, Int, "\\", (obj is byte) ? Convert.ToByte(obj).ToString() : Convert.ToInt32(obj).ToString(), "\\");
                    }
                    flag = true;
                }
                // Handle int[] and uint[] arguments
                else if (obj is int[] || obj is uint[])
                {
                    byte[] array = IntArrayToByte((int[])obj);
                    text = string.Concat(text, ByteArray.ToString(), "/", array.Length, "\\");
                    for (int j = 0; j < array.Length; j++)
                    {
                        text += array[j].ToString("X2");
                    }
                    text += "\\";
                    flag = true;
                }
                // Handle string arguments
                else if (obj is string)
                {
                    string text2 = (string)obj;
                    text = string.Concat(text, ByteArray.ToString(), "/", text2.Length, "\\", ((string)obj).ToHexString(), "\\");
                    flag = true;
                }
                // Handle double arguments
                else if (obj is double num2)
                {
                    text += Float + "\\" + num2 + "\\";
                    flag = true;
                }
                // Handle float arguments
                else if (obj is float num3)
                {
                    text += Float + "\\" + num3 + "\\";
                    flag = true;
                }
                // Handle float[] arguments
                else if (obj is float[])
                {
                    float[] array2 = (float[])obj;
                    text += ByteArray + "/" + array2.Length * 4 + "\\";
                    for (int k = 0; k < array2.Length; k++)
                    {
                        byte[] bytes = BitConverter.GetBytes(array2[k]);
                        Array.Reverse(bytes);
                        for (int l = 0; l < 4; l++)
                        {
                            text += bytes[l].ToString("X2");
                        }
                    }
                    text += "\\";
                    flag = true;
                }
                // Handle byte[] arguments
                else if (obj is byte[])
                {
                    byte[] array3 = (byte[])obj;
                    text = string.Concat(text, ByteArray.ToString(), "/", array3.Length, "\\");
                    for (int m = 0; m < array3.Length; m++)
                    {
                        text += array3[m].ToString("X2");
                    }
                    text += "\\";
                    flag = true;
                }

                // Handle unsupported types
                if (!flag)
                {
                    text += Uint64 + "\\" + ConvertToUInt64(obj) + "\\";
                }
            }

            text += "\"";

            // Send the command and process the response
            string text4 = SendCommand(console, text);
            string text5 = "buf_addr=";
            while (text4.Contains(text5))
            {
                Thread.Sleep(250);
                text4 = SendCommand(console, "consolefeatures " + text5 + "0x" + uint.Parse(text4.Substring(text4.Find(text5) + text5.Length), NumberStyles.HexNumber).ToString("X"));
            }

            console.ConversationTimeout = 2000u;
            console.ConnectTimeout = 5000u;

            // Parse the response based on the type identifier
            switch (Type)
            {
                case 1u:
                    {
                        uint num4 = uint.Parse(text4.Substring(text4.Find(" ") + 1), NumberStyles.HexNumber);
                        if ((object)t == typeof(uint))
                        {
                            return num4;
                        }
                        if ((object)t == typeof(int))
                        {
                            return UIntToInt(num4);
                        }
                        if ((object)t == typeof(short))
                        {
                            return short.Parse(text4.Substring(text4.Find(" ") + 1), NumberStyles.HexNumber);
                        }
                        if ((object)t == typeof(ushort))
                        {
                            return ushort.Parse(text4.Substring(text4.Find(" ") + 1), NumberStyles.HexNumber);
                        }
                        break;
                    }
                case 2u:
                    {
                        string text6 = text4.Substring(text4.Find(" ") + 1);
                        if ((object)t == typeof(string))
                        {
                            return text6;
                        }
                        if ((object)t == typeof(char[]))
                        {
                            return text6.ToCharArray();
                        }
                        break;
                    }
                case 3u:
                    if ((object)t == typeof(double))
                    {
                        return double.Parse(text4.Substring(text4.Find(" ") + 1));
                    }
                    if ((object)t == typeof(float))
                    {
                        return float.Parse(text4.Substring(text4.Find(" ") + 1));
                    }
                    break;
                case 4u:
                    {
                        byte b = byte.Parse(text4.Substring(text4.Find(" ") + 1), NumberStyles.HexNumber);
                        if ((object)t == typeof(byte))
                        {
                            return b;
                        }
                        if ((object)t == typeof(char))
                        {
                            return (char)b;
                        }
                        break;
                    }
                case 8u:
                    if ((object)t == typeof(long))
                    {
                        return long.Parse(text4.Substring(text4.Find(" ") + 1), NumberStyles.HexNumber);
                    }
                    if ((object)t == typeof(ulong))
                    {
                        return ulong.Parse(text4.Substring(text4.Find(" ") + 1), NumberStyles.HexNumber);
                    }
                    break;
            }

            // Handle array types
            switch (Type)
            {
                case 5u:
                    {
                        string text14 = text4.Substring(text4.Find(" ") + 1);
                        int num8 = 0;
                        string text15 = "";
                        uint[] array8 = new uint[8];
                        string text9 = text14;
                        for (int i = 0; i < text9.Length; i++)
                        {
                            char c4 = text9[i];
                            if (c4 != ',' && c4 != ';')
                            {
                                text15 += c4;
                            }
                            else
                            {
                                array8[num8] = uint.Parse(text15, NumberStyles.HexNumber);
                                num8++;
                                text15 = "";
                            }
                            if (c4 == ';')
                            {
                                break;
                            }
                        }
                        return array8;
                    }
                case 6u:
                    {
                        string text12 = text4.Substring(text4.Find(" ") + 1);
                        int num7 = 0;
                        string text13 = "";
                        float[] array7 = new float[ArraySize];
                        string text9 = text12;
                        for (int i = 0; i < text9.Length; i++)
                        {
                            char c3 = text9[i];
                            if (c3 != ',' && c3 != ';')
                            {
                                text13 += c3;
                            }
                            else
                            {
                                array7[num7] = float.Parse(text13);
                                num7++;
                                text13 = "";
                            }
                            if (c3 == ';')
                            {
                                break;
                            }
                        }
                        return array7;
                    }
                case 7u:
                    {
                        string text10 = text4.Substring(text4.Find(" ") + 1);
                        int num6 = 0;
                        string text11 = "";
                        byte[] array6 = new byte[ArraySize];
                        string text9 = text10;
                        for (int i = 0; i < text9.Length; i++)
                        {
                            char c2 = text9[i];
                            if (c2 != ',' && c2 != ';')
                            {
                                text11 += c2;
                            }
                            else
                            {
                                array6[num6] = byte.Parse(text11);
                                num6++;
                                text11 = "";
                            }
                            if (c2 == ';')
                            {
                                break;
                            }
                        }
                        return array6;
                    }
                default:
                    if (Type == Uint64Array)
                    {
                        string text7 = text4.Substring(text4.Find(" ") + 1);
                        int num5 = 0;
                        string text8 = "";
                        ulong[] array4 = new ulong[ArraySize];
                        string text9 = text7;
                        for (int i = 0; i < text9.Length; i++)
                        {
                            char c = text9[i];
                            if (c != ',' && c != ';')
                            {
                                text8 += c;
                            }
                            else
                            {
                                array4[num5] = ulong.Parse(text8);
                                num5++;
                                text8 = "";
                            }
                            if (c == ';')
                            {
                                break;
                            }
                        }
                        if ((object)t == typeof(ulong))
                        {
                            return array4;
                        }
                        if ((object)t == typeof(long))
                        {
                            long[] array5 = new long[ArraySize];
                            for (int n = 0; n < ArraySize; n++)
                            {
                                array5[n] = BitConverter.ToInt64(BitConverter.GetBytes(array4[n]), 0);
                            }
                            return array5;
                        }
                    }
                    if (Type == Void)
                    {
                        return 0;
                    }
                    return ulong.Parse(text4.Substring(text4.Find(" ") + 1), NumberStyles.HexNumber);
            }
        }
        #endregion

        #endregion
    }
}