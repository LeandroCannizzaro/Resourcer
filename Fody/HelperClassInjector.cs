﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

public partial class ModuleWeaver
{
    MethodAttributes staticMethodAttributes =
        MethodAttributes.Public |
        MethodAttributes.HideBySig |
        MethodAttributes.Static;

    void InjectHelper()
    {
        var typeAttributes = TypeAttributes.AnsiClass | TypeAttributes.Sealed  | TypeAttributes.Abstract | TypeAttributes.AutoClass;
        var targetType = new TypeDefinition("Resourcer", "Resource", typeAttributes, ModuleDefinition.TypeSystem.Object);
        ModuleDefinition.Types.Add(targetType);
        var fieldDefinition = new FieldDefinition("assembly", FieldAttributes.Static | FieldAttributes.Private, AssemblyTypeReference)
            {
                DeclaringType = targetType
            };
        targetType.Fields.Add(fieldDefinition);
        InjectConstructor(targetType, fieldDefinition);

        InjectAsStream(targetType, fieldDefinition);
        InjectAsString(targetType, fieldDefinition);
    }

    void InjectAsStream(TypeDefinition targetType, FieldDefinition fieldDefinition)
    {
        var method = new MethodDefinition("AsStream", staticMethodAttributes, StreamTypeReference);
        var pathParam = new ParameterDefinition(ModuleDefinition.TypeSystem.String);
        method.Parameters.Add(pathParam);
        method.Body.InitLocals = true;
        var inst = method.Body.Instructions;
        inst.Add(Instruction.Create(OpCodes.Ldsfld, fieldDefinition));
        inst.Add(Instruction.Create(OpCodes.Ldarg,pathParam));
        inst.Add(Instruction.Create(OpCodes.Callvirt, GetManifestResourceStreamMethod));
        inst.Add(Instruction.Create(OpCodes.Ret));
        targetType.Methods.Add(method);
    }
    void InjectAsString(TypeDefinition targetType, FieldDefinition assemblyField)
    {
        var method = new MethodDefinition("AsString", staticMethodAttributes, ModuleDefinition.TypeSystem.String);
        var pathParam = new ParameterDefinition(ModuleDefinition.TypeSystem.String);
        method.Parameters.Add(pathParam);

        method.Body.InitLocals = true;
        var readerVar = new VariableDefinition(StreamReaderTypeReference);
        method.Body.Variables.Add(readerVar);
        var streamVar = new VariableDefinition(StreamTypeReference);
        method.Body.Variables.Add(streamVar);
        var stringVar = new VariableDefinition(ModuleDefinition.TypeSystem.String);
        method.Body.Variables.Add(stringVar);

        var inst = method.Body.Instructions;

        //33
        var assignStringBeforeReturn = Instruction.Create(OpCodes.Ldloc, stringVar);
        //29
        var assignStreamBeforeDispose = Instruction.Create(OpCodes.Ldloc, streamVar);
        //32
        var endFinally = Instruction.Create(OpCodes.Endfinally);

        inst.Add(Instruction.Create(OpCodes.Ldnull));
        inst.Add(Instruction.Create(OpCodes.Stloc, readerVar));
        inst.Add(Instruction.Create(OpCodes.Ldnull));
        inst.Add(Instruction.Create(OpCodes.Stloc, streamVar));
        var assignAssemblyField = Instruction.Create(OpCodes.Ldsfld, assemblyField);
        inst.Add(assignAssemblyField);
        inst.Add(Instruction.Create(OpCodes.Ldarg, pathParam));
        inst.Add(Instruction.Create(OpCodes.Callvirt, GetManifestResourceStreamMethod));
        inst.Add(Instruction.Create(OpCodes.Stloc, streamVar));
        inst.Add(Instruction.Create(OpCodes.Ldloc, streamVar));
        inst.Add(Instruction.Create(OpCodes.Newobj, StreamReaderConstructorReference));
        inst.Add(Instruction.Create(OpCodes.Stloc, readerVar));
        inst.Add(Instruction.Create(OpCodes.Ldloc, readerVar));
        inst.Add(Instruction.Create(OpCodes.Callvirt, ReadToEndMethod));
        inst.Add(Instruction.Create(OpCodes.Stloc, stringVar));
        inst.Add(Instruction.Create(OpCodes.Leave_S, assignStringBeforeReturn));
        var assignReaderBeforeNullCheck = Instruction.Create(OpCodes.Ldloc, readerVar);
        inst.Add(assignReaderBeforeNullCheck);
        inst.Add(Instruction.Create(OpCodes.Brfalse_S, assignStreamBeforeDispose));
        inst.Add(Instruction.Create(OpCodes.Ldloc, readerVar));
        inst.Add(Instruction.Create(OpCodes.Callvirt, DisposeTextReaderMethod));
        inst.Add(assignStreamBeforeDispose);
        inst.Add(Instruction.Create(OpCodes.Brfalse_S,endFinally));
        inst.Add(Instruction.Create(OpCodes.Ldloc,streamVar));
        inst.Add(Instruction.Create(OpCodes.Callvirt,DisposeStreamMethod));
        inst.Add(endFinally);
        inst.Add(assignStringBeforeReturn);
        inst.Add(Instruction.Create(OpCodes.Ret));

        var finallyHandler = new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = assignAssemblyField,
                TryEnd = assignReaderBeforeNullCheck,
                HandlerStart = assignReaderBeforeNullCheck,
                HandlerEnd = assignStringBeforeReturn
            };
        method.Body.ExceptionHandlers.Add(finallyHandler);
        method.Body.SimplifyMacros();
        targetType.Methods.Add(method);
    }

    void InjectConstructor(TypeDefinition targetType, FieldDefinition fieldDefinition)
    {
        const MethodAttributes attributes = MethodAttributes.Static
                                            | MethodAttributes.SpecialName
                                            | MethodAttributes.RTSpecialName
                                            | MethodAttributes.HideBySig
                                            | MethodAttributes.Private;
        var staticConstructor = new MethodDefinition(".cctor", attributes, ModuleDefinition.TypeSystem.Void);
        targetType.Methods.Add(staticConstructor);
        var instructions = staticConstructor.Body.Instructions;
        instructions.Add(Instruction.Create(OpCodes.Call, GetExecutingAssemblyMethod));
        instructions.Add(Instruction.Create(OpCodes.Stsfld, fieldDefinition));
        instructions.Add(Instruction.Create(OpCodes.Ret));
    }
}