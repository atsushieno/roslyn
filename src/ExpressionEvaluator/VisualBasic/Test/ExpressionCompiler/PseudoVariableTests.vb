﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.ExpressionEvaluator
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.VisualStudio.Debugger.Evaluation
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests
    Public Class PseudoVariableTests
        Inherits ExpressionCompilerTestBase

        Private Const SimpleSource = "
Class C
    Sub M()
    End Sub
End Class
"

        <Fact>
        Public Sub UnrecognizedVariable()
            VerifyError("$v", "(1) : error BC30451: '$v' is not declared. It may be inaccessible due to its protection level.")
        End Sub

        <Fact>
        Public Sub GlobalName()
            VerifyError("Global.$v", "(1) : error BC30456: '$v' is not a member of 'Global'.")
        End Sub

        <Fact>
        Public Sub Qualified()
            VerifyError("Me.$v", "(1) : error BC30456: '$v' is not a member of 'C'.")
        End Sub

        Private Sub VerifyError(expr As String, expectedErrorMessage As String)
            Dim resultProperties As ResultProperties = Nothing
            Dim actualErrorMessage As String = Nothing
            Dim testData = Evaluate(
                            SimpleSource,
                            OutputKind.DynamicallyLinkedLibrary,
                            methodName:="C.M",
                            expr:=expr,
                            resultProperties:=resultProperties,
                            errorMessage:=actualErrorMessage)
            Assert.Equal(expectedErrorMessage, actualErrorMessage)
        End Sub

        <Fact>
        Public Sub Exception()
            Dim comp = CreateCompilationWithMscorlib({SimpleSource}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")

            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()
            Dim result = context.CompileExpression(
                InspectionContextFactory.Empty.Add("$exception", GetType(System.IO.IOException)).Add("$stowedexception", GetType(System.InvalidOperationException)),
                "If($Exception, If($exception, $stowedexception))",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Equal(testData.Methods.Count, 4)

            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       39 (0x27)
  .maxstack  2
  IL_0000:  call       ""Function <>x.$exception() As System.Exception""
  IL_0005:  castclass  ""System.IO.IOException""
  IL_000a:  dup
  IL_000b:  brtrue.s   IL_0026
  IL_000d:  pop
  IL_000e:  call       ""Function <>x.$exception() As System.Exception""
  IL_0013:  castclass  ""System.IO.IOException""
  IL_0018:  dup
  IL_0019:  brtrue.s   IL_0026
  IL_001b:  pop
  IL_001c:  call       ""Function <>x.$stowedexception() As System.Exception""
  IL_0021:  castclass  ""System.InvalidOperationException""
  IL_0026:  ret
}")

            Dim assembly = ImmutableArray.CreateRange(result.Assembly)
            assembly.VerifyIL("<>x.$exception",
"{
  // Code size        2 (0x2)
  .maxstack  8
  IL_0000:  ldnull
  IL_0001:  throw
}")
            assembly.VerifyIL("<>x.$stowedexception",
"{
  // Code size        2 (0x2)
            .maxstack  8
  IL_0000:  ldnull
  IL_0001:  throw
}")
        End Sub

        <Fact>
        Public Sub ReturnValue()
            Dim comp = CreateCompilationWithMscorlib({SimpleSource}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")

            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()
            Dim result = context.CompileExpression(
                InspectionContextFactory.Empty.Add("$ReturnValue", GetType(Object)).Add("$ReturnValue2", GetType(String)),
                "If($ReturnValue, $ReturnValue2)",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       22 (0x16)
  .maxstack  2
  IL_0000:  ldc.i4.0
  IL_0001:  call       ""Function <>x.<>GetReturnValue(Integer) As Object""
  IL_0006:  dup
  IL_0007:  brtrue.s   IL_0015
  IL_0009:  pop
  IL_000a:  ldc.i4.2
  IL_000b:  call       ""Function <>x.<>GetReturnValue(Integer) As Object""
  IL_0010:  castclass  ""String""
  IL_0015:  ret
}")

            Dim assembly = ImmutableArray.CreateRange(result.Assembly)
            assembly.VerifyIL("<>x.<>GetReturnValue",
"{
  // Code size        2 (0x2)
  .maxstack  8
  IL_0000:  ldnull
  IL_0001:  throw
}")

            ' Value type $ReturnValue.
            testData = New CompilationTestData()
            result = context.CompileExpression(
                InspectionContextFactory.Empty.Add("$ReturnValue", GetType(Nullable(Of Integer))),
                "DirectCast($ReturnValue, Integer?).HasValue",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       20 (0x14)
  .maxstack  1
  .locals init (Integer? V_0)
  IL_0000:  ldc.i4.0
  IL_0001:  call       ""Function <>x.<>GetReturnValue(Integer) As Object""
  IL_0006:  unbox.any  ""Integer?""
  IL_000b:  stloc.0
  IL_000c:  ldloca.s   V_0
  IL_000e:  call       ""Function Integer?.get_HasValue() As Boolean""
  IL_0013:  ret
}")
        End Sub

        <Fact>
        Public Sub ReturnValueNegative()
            Const source = "
Class C
    Sub M()
        Microsoft.VisualBasic.VBMath.Randomize()
    End Sub
End Class
"
            Dim testData = Evaluate(
                source,
                OutputKind.DynamicallyLinkedLibrary,
                methodName:="C.M",
                expr:="$returnvalue-2") ' Subtraction, not a negative index.
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       18 (0x12)
  .maxstack  2
  IL_0000:  ldc.i4.0
  IL_0001:  call       ""Function <>x.<>GetReturnValue(Integer) As Object""
  IL_0006:  ldc.i4.2
  IL_0007:  box        ""Integer""
  IL_000c:  call       ""Function Microsoft.VisualBasic.CompilerServices.Operators.SubtractObject(Object, Object) As Object""
  IL_0011:  ret
}")
        End Sub

        <Fact>
        Public Sub ObjectId()
            Const source =
"Class C
    Shared Sub M()
    End Sub
End Class"
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = Evaluate(
                source,
                OutputKind.DynamicallyLinkedLibrary,
                methodName:="C.M",
                expr:="If($23, $4.BaseType)",
                resultProperties:=resultProperties,
                errorMessage:=errorMessage,
                inspection:=InspectionContextFactory.Empty.Add("23", GetType(String)).Add("4", GetType(Type)))
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       40 (0x28)
  .maxstack  2
  IL_0000:  ldstr      ""23""
  IL_0005:  call       ""Function <>x.<>GetObjectByAlias(String) As Object""
  IL_000a:  castclass  ""String""
  IL_000f:  dup
  IL_0010:  brtrue.s   IL_0027
  IL_0012:  pop
  IL_0013:  ldstr      ""4""
  IL_0018:  call       ""Function <>x.<>GetObjectByAlias(String) As Object""
  IL_001d:  castclass  ""System.Type""
  IL_0022:  callvirt   ""Function System.Type.get_BaseType() As System.Type""
  IL_0027:  ret
}")
        End Sub

        <Fact>
        Public Sub PlaceholderMethodNameNormalization()
            Dim comp = CreateCompilationWithMscorlib({SimpleSource}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")

            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()
            Dim result = context.CompileExpression(
                InspectionContextFactory.Empty.Add("$exception", GetType(System.IO.IOException)).Add("$stowedexception", GetType(Exception)),
                "If($ExcEptIOn, $SToWeDeXCePTioN)",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       20 (0x14)
  .maxstack  2
  IL_0000:  call       ""Function <>x.$exception() As System.Exception""
  IL_0005:  castclass  ""System.IO.IOException""
  IL_000a:  dup
  IL_000b:  brtrue.s   IL_0013
  IL_000d:  pop
  IL_000e:  call       ""Function <>x.$stowedexception() As System.Exception""
  IL_0013:  ret
}")

            Dim assembly = ImmutableArray.CreateRange(result.Assembly)
            assembly.VerifyIL("<>x.$exception",
"{
  // Code size        2 (0x2)
  .maxstack  8
  IL_0000:  ldnull
  IL_0001:  throw
}")
            assembly.VerifyIL("<>x.$stowedexception",
"{
  // Code size        2 (0x2)
  .maxstack  8
  IL_0000:  ldnull
  IL_0001:  throw
}")
        End Sub

        <WorkItem(1101017)>
        <Fact>
        Public Sub NestedGenericValueType()
            Const source =
"Class C
    Friend Structure S(Of T)
        Friend F As T
    End Structure
    Shared Sub M()
    End Sub
End Class"
            Dim comp = CreateCompilationWithMscorlib({source}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("s", "C+S`1[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]"),
                "s.F + 1",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       23 (0x17)
  .maxstack  2
  IL_0000:  ldstr      ""s""
  IL_0005:  call       ""Function <>x.<>GetObjectByAlias(String) As Object""
  IL_000a:  unbox.any  ""C.S(Of Integer)""
  IL_000f:  ldfld      ""C.S(Of Integer).F As Integer""
  IL_0014:  ldc.i4.1
  IL_0015:  add.ovf
  IL_0016:  ret
}")
        End Sub

        <Fact>
        Public Sub ArrayType()
            Const source =
"Class C
    Friend F As Object
    Shared Sub M()
    End Sub
End Class"
            Dim comp = CreateCompilationWithMscorlib({source}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("a", "C[]").Add("b", "System.Int32[,], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                "a(b(1, 0)).F",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       44 (0x2c)
  .maxstack  4
  IL_0000:  ldstr      ""a""
  IL_0005:  call       ""Function <>x.<>GetObjectByAlias(String) As Object""
  IL_000a:  castclass  ""C()""
  IL_000f:  ldstr      ""b""
  IL_0014:  call       ""Function <>x.<>GetObjectByAlias(String) As Object""
  IL_0019:  castclass  ""Integer(,)""
  IL_001e:  ldc.i4.1
  IL_001f:  ldc.i4.0
  IL_0020:  call       ""Integer(*,*).Get""
  IL_0025:  ldelem.ref
  IL_0026:  ldfld      ""C.F As Object""
  IL_002b:  ret
}")
        End Sub

        ''' <summary>
        ''' The assembly-qualified type name may be from an
        ''' unrecognized assembly. For instance, if the type was
        ''' defined in a previous evaluation, say an anonymous
        ''' type (e.g.: evaluate "o" after "o = New With { .P = 1 }").
        ''' </summary>
        <WorkItem(1102923)>
        <Fact(Skip:="1102923")>
        Public Sub UnrecognizedAssembly()
            Const source =
"Friend Structure S(Of T)
    Friend F As T
End Structure
Class C
    Shared Sub M()
    End Sub
End Class"
            Dim comp = CreateCompilationWithMscorlib({source}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()

            ' Unrecognized type.
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("o", "T, 9BAC6622-86EB-4EC5-94A1-9A1E6D0C25AB, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"),
                "o.P",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Equal(errorMessage, "...")

            ' Unrecognized array element type.
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("a", "T[], 9BAC6622-86EB-4EC5-94A1-9A1E6D0C25AB, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"),
                "a(0).P",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Equal(errorMessage, "...")

            ' Unrecognized generic type argument.
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("s", "S`1[[T, 9BAC6622-86EB-4EC5-94A1-9A1E6D0C25AB, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]"),
                "s.F",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Equal(errorMessage, "...")
        End Sub

        <Fact>
        Public Sub Variables()
            CheckVariable("$exception", valid:=True, methodNames:={"<>x.$exception()"})
            CheckVariable("$eXCePTioN", valid:=True, methodNames:={"<>x.$exception()"})
            CheckVariable("$stowedexception", valid:=True, methodNames:={"<>x.$stowedexception()"})
            CheckVariable("$stOwEdExcEptIOn", valid:=True, methodNames:={"<>x.$stowedexception()"})
            CheckVariable("$ReturnValue", valid:=True, methodNames:={"<>x.<>GetReturnValue(Integer)"})
            CheckVariable("$rEtUrnvAlUe", valid:=True, methodNames:={"<>x.<>GetReturnValue(Integer)"})
            CheckVariable("$ReturnValue0", valid:=True, methodNames:={"<>x.<>GetReturnValue(Integer)"})
            CheckVariable("$ReturnValue21", valid:=True, methodNames:={"<>x.<>GetReturnValue(Integer)"})
            CheckVariable("$ReturnValue3A", valid:=False)
            CheckVariable("$33", valid:=True, methodNames:={"<>x.<>GetObjectByAlias(String)", "<>x.<>GetVariableAddress(Of <>T)(String)"})
            CheckVariable("$03", valid:=False)
            CheckVariable("$3A", valid:=False)
            CheckVariable("$0", valid:=False)
            CheckVariable("$", valid:=False)
            CheckVariable("$Unknown", valid:=False)
        End Sub

        Private Sub CheckVariable(variableName As String, valid As Boolean, Optional methodNames As String() = Nothing)
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = Evaluate(
                SimpleSource,
                OutputKind.DynamicallyLinkedLibrary,
                methodName:="C.M",
                expr:=variableName,
                resultProperties:=resultProperties,
                errorMessage:=errorMessage)

            If valid Then
                Dim builder = ArrayBuilder(Of String).GetInstance()
                builder.Add("<>x.<>m0(C)")
                If methodNames IsNot Nothing Then
                    builder.AddRange(methodNames)
                End If
                builder.Add("<invalid-global-code>..ctor()") ' Unnecessary <invalid-global-code> (DevDiv #1010243)
                Dim expectedNames = builder.ToImmutableAndFree()
                Dim actualNames = testData.Methods.Keys
                AssertEx.SetEqual(expectedNames, actualNames)
            Else
                Assert.Equal(
                    String.Format("(1) : error BC30451: '{0}' is not declared. It may be inaccessible due to its protection level.", variableName),
                    errorMessage)
            End If
        End Sub

        <Fact>
        Public Sub CheckViability()
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = Evaluate(
                SimpleSource,
                OutputKind.DynamicallyLinkedLibrary,
                methodName:="C.M",
                expr:="$ReturnValue(Of Object)",
                resultProperties:=resultProperties,
                errorMessage:=errorMessage)
            Assert.Equal("(1) : error BC32045: '$ReturnValue' has no type parameters and so cannot have type arguments.", errorMessage)

            Const source = "
Class C
    Sub M()
        Microsoft.VisualBasic.VBMath.Randomize()
    End Sub
End Class
"

            ' Since the type of $ReturnValue2 is object, late binding will be attempted.
            testData = Evaluate(
                source,
                OutputKind.DynamicallyLinkedLibrary,
                methodName:="C.M",
                expr:="$ReturnValue2()",
                resultProperties:=resultProperties,
                errorMessage:=errorMessage)
            Assert.Null(errorMessage)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       19 (0x13)
  .maxstack  3
  IL_0000:  ldc.i4.2
  IL_0001:  call       ""Function <>x.<>GetReturnValue(Integer) As Object""
  IL_0006:  ldc.i4.0
  IL_0007:  newarr     ""Object""
  IL_000c:  ldnull
  IL_000d:  call       ""Function Microsoft.VisualBasic.CompilerServices.NewLateBinding.LateIndexGet(Object, Object(), String()) As Object""
  IL_0012:  ret
}")
        End Sub

        ''' <summary>
        ''' $exception may be accessed from closure class.
        ''' </summary>
        <Fact>
        Public Sub ExceptionInDisplayClass()
            Const source = "
Imports System

Class C
    Shared Function F(f1 as System.Func(Of Object)) As Object
        Return f1()
    End Function
    
    Shared Sub M(o As Object)
    End Sub
End Class
"
            Dim testData = Evaluate(
                source,
                OutputKind.DynamicallyLinkedLibrary,
                methodName:="C.M",
                expr:="F(Function() If(o, $exception))")
            testData.GetMethodData("<>x._Closure$__0-0._Lambda$__1()").VerifyIL(
"{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""<>x._Closure$__0-0.$VB$Local_o As Object""
  IL_0006:  dup
  IL_0007:  brtrue.s   IL_000f
  IL_0009:  pop
  IL_000a:  call       ""Function <>x.$exception() As System.Exception""
  IL_000f:  ret
}")
        End Sub

        <Fact>
        Public Sub AssignException()
            Const source = "
Class C
    Shared Sub M(e As System.Exception)
    End Sub
End Class
"
            Dim comp = CreateCompilationWithMscorlib({source}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()
            context.CompileAssignment("e", "If($exception.InnerException, $exception)", errorMessage, testData, VisualBasicDiagnosticFormatter.Instance)
            Assert.Null(errorMessage)
            testData.GetMethodData("<>x.<>m0").VerifyIL("
{
  // Code size       22 (0x16)
  .maxstack  2
  IL_0000:  call       ""Function <>x.$exception() As System.Exception""
  IL_0005:  callvirt   ""Function System.Exception.get_InnerException() As System.Exception""
  IL_000a:  dup
  IL_000b:  brtrue.s   IL_0013
  IL_000d:  pop
  IL_000e:  call       ""Function <>x.$exception() As System.Exception""
  IL_0013:  starg.s    V_0
  IL_0015:  ret
}
")
        End Sub

        <Fact>
        Public Sub AssignToException()
            Dim comp = CreateCompilationWithMscorlib({SimpleSource}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData = New CompilationTestData()
            context.CompileAssignment("$exception", "Nothing", errorMessage, testData, VisualBasicDiagnosticFormatter.Instance)
            ' CONSIDER: ERR_LValueRequired would be clearer.
            Assert.Equal("(1) : error BC30064: 'ReadOnly' variable cannot be the target of an assignment.", errorMessage)
        End Sub

        <WorkItem(1100849)>
        <Fact>
        Public Sub PassByRef()
            Const source = "
Class C
    Shared Function F(Of T)(ByRef t1 As T) As T
        t1 = Nothing
        Return t1
    End Function    
End Class
"
            Dim comp = CreateCompilationWithMscorlib({source}, compOptions:=TestOptions.DebugDll, assemblyName:=ExpressionCompilerUtilities.GenerateUniqueName())
            Dim runtime = CreateRuntimeInstance(comp)
            Dim context = CreateMethodContext(runtime, "C.F")
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing

            ' $exception
            Dim testData = New CompilationTestData()
            context.CompileExpression(
                DefaultInspectionContext.Instance,
                "$exception = Nothing",
                DkmEvaluationFlags.None,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Equal(errorMessage, "(1,1): error BC30064: 'ReadOnly' variable cannot be the target of an assignment.")
            testData = New CompilationTestData()
            context.CompileExpression(
                DefaultInspectionContext.Instance,
                "F($exception)",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Null(errorMessage)
            ' In VB, non-l-values can be passed by ref - we
            ' just synthesize a temp and pass that.
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       14 (0xe)
  .maxstack  1
  .locals init (T V_0, //F
  System.Exception V_1)
  IL_0000:  call       ""Function <>x.$exception() As System.Exception""
  IL_0005:  stloc.1
  IL_0006:  ldloca.s   V_1
  IL_0008:  call       ""Function C.F(Of System.Exception)(ByRef System.Exception) As System.Exception""
  IL_000d:  ret
}")

            ' $ReturnValue
            testData = New CompilationTestData()
            context.CompileExpression(
                DefaultInspectionContext.Instance,
                "$ReturnValue = Nothing",
                DkmEvaluationFlags.None,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Equal(errorMessage, "(1,1): error BC30064: 'ReadOnly' variable cannot be the target of an assignment.")
            testData = New CompilationTestData()
            context.CompileExpression(
                DefaultInspectionContext.Instance,
                "F($ReturnValue)",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Null(errorMessage)

            ' Object id
            testData = New CompilationTestData()
            context.CompileExpression(
                DefaultInspectionContext.Instance,
                "$1 = Nothing",
                DkmEvaluationFlags.None,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Equal(errorMessage, "(1,1): error BC30064: 'ReadOnly' variable cannot be the target of an assignment.")
            testData = New CompilationTestData()
            context.CompileExpression(
                DefaultInspectionContext.Instance,
                "F($1)",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Null(errorMessage)

            ' Existing pseudo-variable
            testData = New CompilationTestData()
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("x", GetType(Integer)),
                "x = Nothing",
                DkmEvaluationFlags.None,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Null(errorMessage)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       13 (0xd)
  .maxstack  2
  .locals init (T V_0) //F
  IL_0000:  ldstr      ""x""
  IL_0005:  call       ""Function <>x.<>GetVariableAddress(Of Integer)(String) As Integer""
  IL_000a:  ldc.i4.0
  IL_000b:  stind.i4
  IL_000c:  ret
}")
            testData = New CompilationTestData()
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("x", GetType(Integer)),
                "F(x)",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Null(errorMessage)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       16 (0x10)
  .maxstack  1
  .locals init (T V_0) //F
  IL_0000:  ldstr      ""x""
  IL_0005:  call       ""Function <>x.<>GetVariableAddress(Of Integer)(String) As Integer""
  IL_000a:  call       ""Function C.F(Of Integer)(ByRef Integer) As Integer""
  IL_000f:  ret
}")

            ' Implicitly declared variable
            testData = New CompilationTestData()
            context.CompileExpression(
                InspectionContextFactory.Empty,
                "x = Nothing",
                DkmEvaluationFlags.None,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Null(errorMessage)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       33 (0x21)
  .maxstack  2
  .locals init (T V_0) //F
  IL_0000:  ldtoken    ""Object""
  IL_0005:  call       ""Function System.Type.GetTypeFromHandle(System.RuntimeTypeHandle) As System.Type""
  IL_000a:  ldstr      ""x""
  IL_000f:  call       ""Sub <>x.<>CreateVariable(System.Type, String)""
  IL_0014:  ldstr      ""x""
  IL_0019:  call       ""Function <>x.<>GetVariableAddress(Of Object)(String) As Object""
  IL_001e:  ldnull
  IL_001f:  stind.ref
  IL_0020:  ret
}")
            testData = New CompilationTestData()
            context.CompileExpression(
                InspectionContextFactory.Empty,
                "F(x)",
                DkmEvaluationFlags.None,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            Assert.Null(errorMessage)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       37 (0x25)
  .maxstack  2
  .locals init (T V_0) //F
  IL_0000:  ldtoken    ""Object""
  IL_0005:  call       ""Function System.Type.GetTypeFromHandle(System.RuntimeTypeHandle) As System.Type""
  IL_000a:  ldstr      ""x""
  IL_000f:  call       ""Sub <>x.<>CreateVariable(System.Type, String)""
  IL_0014:  ldstr      ""x""
  IL_0019:  call       ""Function <>x.<>GetVariableAddress(Of Object)(String) As Object""
  IL_001e:  call       ""Function C.F(Of Object)(ByRef Object) As Object""
  IL_0023:  pop
  IL_0024:  ret
}")
            testData.GetMethodData("<>x.<>GetVariableAddress(Of <>T)").VerifyIL(
"{
  // Code size        2 (0x2)
  .maxstack  1
  IL_0000:  ldnull
  IL_0001:  throw
}")
        End Sub

        ''' <summary>
        ''' Assembly-qualified type names from the debugger refer to runtime assemblies
        ''' which may be different versions than the assembly references in metadata.
        ''' </summary>
        <WorkItem(1087458)>
        <Fact>
        Public Sub DifferentAssemblyVersion()
            Const sourceA =
"Public Class A(Of T)
End Class"
            Const sourceB =
"Class B(Of T)
End Class
Class C
    Shared Sub M()
        Dim o As New A(Of Object)()
    End Sub
End Class"
            Dim assemblyNameA = "397300B1-A"
            Dim publicKeyA = ImmutableArray.CreateRange(Of Byte)({&H00, &H24, &H00, &H00, &H04, &H80, &H00, &H00, &H94, &H00, &H00, &H00, &H06, &H02, &H00, &H00, &H00, &H24, &H00, &H00, &H52, &H53, &H41, &H31, &H00, &H04, &H00, &H00, &H01, &H00, &H01, &H00, &HED, &HD3, &H22, &HCB, &H6B, &HF8, &HD4, &HA2, &HFC, &HCC, &H87, &H37, &H04, &H06, &H04, &HCE, &HE7, &HB2, &HA6, &HF8, &H4A, &HEE, &HF3, &H19, &HDF, &H5B, &H95, &HE3, &H7A, &H6A, &H28, &H24, &HA4, &H0A, &H83, &H83, &HBD, &HBA, &HF2, &HF2, &H52, &H20, &HE9, &HAA, &H3B, &HD1, &HDD, &HE4, &H9A, &H9A, &H9C, &HC0, &H30, &H8F, &H01, &H40, &H06, &HE0, &H2B, &H95, &H62, &H89, &H2A, &H34, &H75, &H22, &H68, &H64, &H6E, &H7C, &H2E, &H83, &H50, &H5A, &HCE, &H7B, &H0B, &HE8, &HF8, &H71, &HE6, &HF7, &H73, &H8E, &HEB, &H84, &HD2, &H73, &H5D, &H9D, &HBE, &H5E, &HF5, &H90, &HF9, &HAB, &H0A, &H10, &H7E, &H23, &H48, &HF4, &HAD, &H70, &H2E, &HF7, &HD4, &H51, &HD5, &H8B, &H3A, &HF7, &HCA, &H90, &H4C, &HDC, &H80, &H19, &H26, &H65, &HC9, &H37, &HBD, &H52, &H81, &HF1, &H8B, &HCD})
            Dim compilationA1 = CreateCompilation(
                New AssemblyIdentity(assemblyNameA, New Version(1, 1, 1, 1), cultureName:="", publicKeyOrToken:=publicKeyA, hasPublicKey:=True),
                {sourceA},
                references:={MscorlibRef_v20},
                options:=TestOptions.DebugDll.WithDelaySign(True))
            Dim referenceA1 = compilationA1.EmitToImageReference()
            Dim assemblyNameB = "397300B1-B"
            Dim compilationB1 = CreateCompilation(
                New AssemblyIdentity(assemblyNameB, New Version(1, 2, 2, 2)),
                {sourceB},
                references:={MscorlibRef_v20, referenceA1},
                options:=TestOptions.DebugDll)

            ' Use mscorlib v4.0.0.0 and A v2.1.2.1 at runtime.
            Dim exeBytes As Byte() = Nothing
            Dim pdbBytes As Byte() = Nothing
            Dim references As ImmutableArray(Of MetadataReference) = Nothing
            compilationB1.EmitAndGetReferences(exeBytes, pdbBytes, references)
            Dim compilationA2 = CreateCompilation(
                New AssemblyIdentity(assemblyNameA, New Version(2, 1, 2, 1), cultureName:="", publicKeyOrToken:=publicKeyA, hasPublicKey:=True),
                {sourceA},
                references:={MscorlibRef_v20},
                options:=TestOptions.DebugDll.WithDelaySign(True))
            Dim referenceA2 = compilationA2.EmitToImageReference()
            Dim runtime = CreateRuntimeInstance(
                assemblyNameB,
                ImmutableArray.Create(MscorlibRef, referenceA2),
                exeBytes,
                New SymReader(pdbBytes))

            Dim context = CreateMethodContext(runtime, "C.M")
            Dim resultProperties As ResultProperties = Nothing
            Dim errorMessage As String = Nothing
            Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
            Dim testData As New CompilationTestData()
            ' GetType(Exception), GetType(A(Of B(Of Object))), GetType(B(Of A(Of Object)()))
            context.CompileExpression(
                InspectionContextFactory.Empty.Add("$exception", "System.Exception, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089").
                    Add("1", "A`1[[B`1[[System.Object, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], 397300B1-B, Version=1.2.2.2, Culture=neutral, PublicKeyToken=null]], 397300B1-A, Version=2.1.2.1, Culture=neutral, PublicKeyToken=null").
                    Add("2", "B`1[[A`1[[System.Object, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]][], 397300B1-A, Version=2.1.2.1, Culture=neutral, PublicKeyToken=null]], 397300B1-B, Version=1.2.2.2, Culture=neutral, PublicKeyToken=null"),
                "If(If($exception, $1), $2)",
                DkmEvaluationFlags.TreatAsExpression,
                DiagnosticFormatter.Instance,
                resultProperties,
                errorMessage,
                missingAssemblyIdentities,
                EnsureEnglishUICulture.PreferredOrNull,
                testData)
            Assert.Empty(missingAssemblyIdentities)
            testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
  // Code size       44 (0x2c)
  .maxstack  2
  .locals init (A(Of Object) V_0) //o
  IL_0000:  call       ""Function <>x.$exception() As System.Exception""
  IL_0005:  dup
  IL_0006:  brtrue.s   IL_0018
  IL_0008:  pop
  IL_0009:  ldstr      ""1""
  IL_000e:  call       ""Function <>x.<>GetObjectByAlias(String) As Object""
  IL_0013:  castclass  ""A(Of B(Of Object))""
  IL_0018:  dup
  IL_0019:  brtrue.s   IL_002b
  IL_001b:  pop
  IL_001c:  ldstr      ""2""
  IL_0021:  call       ""Function <>x.<>GetObjectByAlias(String) As Object""
  IL_0026:  castclass  ""B(Of A(Of Object)())""
  IL_002b:  ret
}")
        End Sub

        ''' <summary>
        ''' The assembly-qualified type may reference an assembly
        ''' outside of the current module and its references.
        ''' </summary>
        <WorkItem(1092680)>
        <Fact>
        Public Sub TypeOutsideModule()
            Const sourceA =
"Imports System
Public Class A(Of T)
    Public Shared Sub M(f As Action)
        Dim o As Object
        Try
            f()
        Catch e As Exception
        End Try
    End Sub
End Class"
            Const sourceB =
"Imports System
Class E
    Inherits Exception
    Friend F As Object
End Class
Class B
    Shared Sub Main()
        A(Of Integer).M(Sub()
                Throw New E()
            End Sub)
    End Sub
End Class"
            Dim assemblyNameA = "0B93FF0B-31A2-47C8-B24D-16A2D77AB5C5"
            Dim compilationA = CreateCompilationWithMscorlibAndVBRuntime(MakeSources(sourceA, assemblyName:=assemblyNameA), options:=TestOptions.DebugDll)
            Dim exeA As Byte() = Nothing
            Dim pdbA As Byte() = Nothing
            Dim referencesA As ImmutableArray(Of MetadataReference) = Nothing
            compilationA.EmitAndGetReferences(exeA, pdbA, referencesA)
            Dim referenceA = AssemblyMetadata.CreateFromImage(exeA).GetReference()

            Dim assemblyNameB = "9BBC6622-86EB-4EC5-94A1-9A1E6D0C24B9"
            Dim compilationB = CreateCompilationWithMscorlibAndVBRuntimeAndReferences(MakeSources(sourceB, assemblyName:=assemblyNameB), options:=TestOptions.DebugDll, additionalRefs:={referenceA})
            Dim exeB As Byte() = Nothing
            Dim pdbB As Byte() = Nothing
            Dim referencesB As ImmutableArray(Of MetadataReference) = Nothing
            compilationB.EmitAndGetReferences(exeB, pdbB, referencesB)
            Dim referenceB = AssemblyMetadata.CreateFromImage(exeB).GetReference()

            Dim modulesBuilder = ArrayBuilder(Of ModuleInstance).GetInstance()
            modulesBuilder.Add(MscorlibRef.ToModuleInstance(fullImage:=Nothing, symReader:=Nothing))
            modulesBuilder.Add(referenceA.ToModuleInstance(fullImage:=exeA, symReader:=New SymReader(pdbA)))
            modulesBuilder.Add(referenceB.ToModuleInstance(fullImage:=exeB, symReader:=New SymReader(pdbB)))

            Using runtime = New RuntimeInstance(modulesBuilder.ToImmutableAndFree())
                Dim context = CreateMethodContext(runtime, "A.M")
                Dim resultProperties As ResultProperties = Nothing
                Dim errorMessage As String = Nothing
                Dim missingAssemblyIdentities As ImmutableArray(Of AssemblyIdentity) = Nothing
                Dim testData = New CompilationTestData()
                context.CompileExpression(
                    InspectionContextFactory.Empty.Add("$exception", "E, 9BBC6622-86EB-4EC5-94A1-9A1E6D0C24B9, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    "$exception",
                    DkmEvaluationFlags.TreatAsExpression,
                    DiagnosticFormatter.Instance,
                    resultProperties,
                    errorMessage,
                    missingAssemblyIdentities,
                    EnsureEnglishUICulture.PreferredOrNull,
                    testData)
                Assert.Empty(missingAssemblyIdentities)
                testData.GetMethodData("<>x(Of T).<>m0").VerifyIL(
"{
  // Code size       11 (0xb)
  .maxstack  1
  .locals init (Object V_0, //o
                System.Exception V_1)
  IL_0000:  call       ""Function <>x(Of T).$exception() As System.Exception""
  IL_0005:  castclass  ""E""
  IL_000a:  ret
}")
                testData = New CompilationTestData()
                context.CompileAssignment(
                    InspectionContextFactory.Empty.Add("1", "A`1[[B, 9BBC6622-86EB-4EC5-94A1-9A1E6D0C24B9, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], 0B93FF0B-31A2-47C8-B24D-16A2D77AB5C5, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    "o",
                    "$1",
                    DiagnosticFormatter.Instance,
                    resultProperties,
                    errorMessage,
                    missingAssemblyIdentities,
                    EnsureEnglishUICulture.PreferredOrNull,
                    testData)
                Assert.Empty(missingAssemblyIdentities)
                testData.GetMethodData("<>x(Of T).<>m0").VerifyIL(
"{
  // Code size       17 (0x11)
  .maxstack  1
  .locals init (Object V_0, //o
                System.Exception V_1)
  IL_0000:  ldstr      ""1""
  IL_0005:  call       ""Function <>x(Of T).<>GetObjectByAlias(String) As Object""
  IL_000a:  castclass  ""A(Of B)""
  IL_000f:  stloc.0
  IL_0010:  ret
}")
            End Using
        End Sub

    End Class
End Namespace