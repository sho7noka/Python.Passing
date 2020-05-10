import clr

from clr_ext import AddReference

_template = """
// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version: %s
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
"""


def gen_cs(code, file_name):
    """

    Args:
        file_name:  
        code: 

    Returns:

    """
    AddReference("System")

    from System import IO
    from System import Text
    from System import CodeDom as dom
    from System.CodeDom import Compiler

    nameSpace = dom.CodeNamespace("Client")
    comment = dom.CodeCommentStatement(dom.CodeComment(_template))
    nameSpace.Comments.Add(comment)

    nameSpace.Imports.Add(dom.CodeNamespaceImport("System"))
    nameSpace.Imports.Add(dom.CodeNamespaceImport("UnityEngine"))
    nameSpace.Imports.Add(dom.CodeNamespaceImport("UnityEditor"))

    mainClass = dom.CodeTypeDeclaration("Runtime")
    # MonoBehaivor, ScriptableObject, AssetPostprocessror, ShaderGui
    # SerializeField 
    # Node
    # AssetDatabase.Reflesh()
    mainClass.BaseTypes.Add(dom.CodeTypeReference(clr.GetClrType(UnityEngine.MonoBehaviour)))
    nameSpace.Types.Add(mainClass)

    # fields = code.GetFields(
    #     ref.BindingFlags.GetField | ref.BindingFlags.Public | ref.BindingFlags.Instance
    # )
    # 
    # for field in fields:
    #     variable = dom.CodeMemberField(field.FieldType.Name, field.Name)
    #     mainClass.Members.Add(variable)
    # 
    # methods = code.GetMethods(
    #     ref.BindingFlags.Public | ref.BindingFlags.NonPublic |
    #     ref.BindingFlags.Instance | ref.BindingFlags.Static | ref.BindingFlags.DeclaredOnly
    # )
    # 
    # for method in methods:
    #     mainMethod = dom.CodeMemberMethod()
    #     mainMethod.ReturnType = dom.CodeTypeReference(method.ReturnType)
    #     mainMethod.Attributes = dom.MemberAttributes.Public | dom.MemberAttributes.Final
    #     mainMethod.Name = method.Name
    # 
    #     mainMethod.Comments.Add(dom.CodeCommentStatement("doc comment"))
    # 
    #     if (method.Attributes & dom.MethodAttributes.Static) != 0:
    #         target = dom.CodeSnippetExpression(code.FullName)
    #     else:
    #         target = dom.CodeObjectCreateExpression(code.FullName)
    # 
    #     invoke = dom.CodeMethodInvokeExpression(target, method.Name)
    # 
    #     for p in method.GetParameters():
    #         invoke.Parameters.Add(dom.CodeArgumentReferenceExpression(p.Name))
    #         exp = dom.CodeParameterDeclarationExpression(p.ParameterType, p.Name)
    #         mainMethod.Parameters.Add(exp)
    # 
    #     if method.ReturnType.Name != "Void":
    #         mainMethod.Statements.Add(dom.CodeMethodReturnStatement(invoke))
    #     else:
    #         mainMethod.Statements.Add(invoke)
    # 
    #     mainClass.Members.Add(mainMethod)

    codeText = Text.StringBuilder()
    compilerOptions = Compiler.CodeGeneratorOptions()
    compilerOptions.IndentString = "    "
    compilerOptions.BracingStyle = "C#"

    # Csharp に書き込み
    codeWriter = IO.StringWriter(codeText)
    Compiler.CodeDomProvider.CreateProvider("C#").GenerateCodeFromNamespace(nameSpace, codeWriter, compilerOptions)
    codeWriter.Close()

    writer = IO.StreamWriter(file_name)
    writer.Write(codeText)
    writer.Close()