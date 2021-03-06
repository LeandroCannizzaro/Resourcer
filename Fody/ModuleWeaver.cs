﻿using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public partial class ModuleWeaver
{
    public Action<string> LogInfo { get; set; }
    public Action<string, SequencePoint> LogErrorPoint { get; set; }
    public ModuleDefinition ModuleDefinition { get; set; }
    public IAssemblyResolver AssemblyResolver { get; set; }
    public string ProjectDirectoryPath { get; set; }

    public ModuleWeaver()
    {
        LogInfo = s => { };
        LogErrorPoint = (s,p) => { };
    }

    public void Execute()
    {
        FindCoreReferences();
        InjectHelper();
        foreach (var type in ModuleDefinition
            .GetTypes()
            .Where(x => x.IsClass()))
        {

            foreach (var method in type.Methods)
            {
                //skip for abstract and delegates
                if (!method.HasBody)
                {
                    continue;
                }
                Process(method);
            }
        }
        CleanReferences();
    }

}