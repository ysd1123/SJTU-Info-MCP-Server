using Python.Runtime;
using System;

namespace SJTUGeek.MCP.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Runtime.PythonDLL = @"python310.dll";
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();

            using (Py.GIL())
            {
                using (PyModule scope = Py.CreateScope())
                {
                    PyObject test = Py.Import("scripts.add");
                    // convert the Person object to a PyObject
                    PyObject pyPerson = 11.ToPython();

                    // create a Python variable "person"
                    scope.Set("person", pyPerson);

                    // the person object may now be used in Python
                    string code = "person ** 2";
                    dynamic res = scope.Eval(code);

                    //PyObject func;
                    //func.Invoke();
                    //true.ToPython();
                }
            }
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
            PythonEngine.Shutdown();
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false);
        }
    }
}
