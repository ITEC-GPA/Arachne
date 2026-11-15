using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

namespace Gpc.PluginChecks
{
    // Identical source shared by BridGHe, ReBear and Arachne; C# 5 / .NET Framework compatible.
    // Informational only. No resolver, preload, component warning or numerical acceptance rule.
    internal static class MathNetRuntimeInfo
    {
        private const string StartedKey = "Gpc.PluginChecks.MathNetRuntimeInfo.20261115.Started";
        private static readonly ConcurrentQueue<Assembly> Pending = new ConcurrentQueue<Assembly>();
        private static readonly ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();
        private static readonly HashSet<Assembly> Seen = new HashSet<Assembly>();
        private static int working;

        internal static void Start()
        {
            // All three plug-ins run in the same AppDomain. Only the first copy subscribes.
            // This is PriorityLoad, never the AssemblyLoad callback.
            lock (AppDomain.CurrentDomain)
            {
                if (AppDomain.CurrentDomain.GetData(StartedKey) != null) return;
                AppDomain.CurrentDomain.SetData(StartedKey, true);
            }
            AppDomain.CurrentDomain.AssemblyLoad += Loaded;
            Rhino.RhinoApp.Idle += Idle;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) Pending.Enqueue(assembly);
        }

        private static void Loaded(object sender, AssemblyLoadEventArgs args)
        {
            // No hash, file access, Rhino dispatch, WriteLine, reflection scan or waiting here.
            Pending.Enqueue(args.LoadedAssembly);
        }

        private static void Idle(object sender, EventArgs args)
        {
            string message;
            while (Messages.TryDequeue(out message)) Rhino.RhinoApp.WriteLine(message);
            if (Pending.IsEmpty || Interlocked.CompareExchange(ref working, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(Scan);
        }

        private static void Scan(object ignored)
        {
            try
            {
                Assembly assembly;
                while (Pending.TryDequeue(out assembly))
                {
                    if (!Seen.Add(assembly)) continue;
                    try
                    {
                        if (!string.Equals(assembly.GetName().Name, "MathNet.Numerics", StringComparison.Ordinal)) continue;
                        string path = assembly.IsDynamic ? null : assembly.Location;
                        string hash = "unavailable (no file location)";
                        if (!string.IsNullOrEmpty(path))
                        {
                            try
                            {
                                using (var stream = File.OpenRead(path))
                                using (var digest = SHA256.Create())
                                    hash = BitConverter.ToString(digest.ComputeHash(stream)).Replace("-", "");
                            }
                            catch (Exception error) { hash = "unavailable (" + error.GetType().Name + ")"; }
                        }
                        Messages.Enqueue("MathNet loaded: " + (path ?? "[no file location]") + "; SHA-256: " + hash);
                    }
                    catch (Exception error)
                    {
                        // This does not establish an assembly mismatch or a numerical error.
                        Messages.Enqueue("MathNet runtime inspection unavailable: " + error.GetType().Name);
                    }
                }
            }
            finally { Interlocked.Exchange(ref working, 0); }
        }
    }
}
