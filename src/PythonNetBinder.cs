using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using System.Text;

#if UNITY
using UnityEngine;
using UnityEditor;
#elif GODOT
using Godot;
#endif

namespace Python.Passing
{
    /**
     * TODO: レガシー
     * 指定namespace.class以下を取得
     * https://mitosuya.net/execute-all-class-in-namespace
     */
    public class PythonNetBinder
    {
        private static CodeNamespace nameSpace;

        private const string _template = @"
// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:
//     Date Time:
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
        ";

        public static PythonNetBinder Gen(Type typeName)
        {
            nameSpace = new CodeNamespace(name: "Client");
            var comment = new CodeCommentStatement(new CodeComment(_template));
            nameSpace.Comments.Add(comment);

            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "System"));
#if GODOT
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "Godot"));
#elif INHOUSE
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: ""));
#endif

            // class Runtime {
            var mainClass = new CodeTypeDeclaration(name: "Runtime");
#if UNITY
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "UnityEngine"));
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "UnityEditor"));
            mainClass.BaseTypes.Add(new CodeTypeReference(typeof(MonoBehaviour)));
#endif
            var fields = typeName.GetFields(BindingFlags.GetField | BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var variable = new CodeMemberField(field.FieldType.Name, field.Name);
#if UNITY
                var codeAttrDecl = new CodeAttributeDeclaration(
                    "SerializeField",
                    new CodeAttributeArgument(new CodePrimitiveExpression(false)));
                variable.CustomAttributes.Add(codeAttrDecl);
#endif
                mainClass.Members.Add(variable);
            }

            var methods = typeName.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var mainMethod = new CodeMemberMethod
                {
                    ReturnType = new CodeTypeReference(method.ReturnType),
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    Name = method.Name
                };
                // var docString = "docstring";
                // var docAttr = new CodeAttributeDeclaration(
                //     "DocStringAttribute",
                //     new CodeAttributeArgument(new CodePrimitiveExpression(true)));
                // mainMethod.CustomAttributes.Add(docAttr);
                // mainMethod.Comments.Add(new CodeCommentStatement(docString));

                CodeExpression target;
                if ((method.Attributes & MethodAttributes.Static) != 0)
                    target = new CodeSnippetExpression(typeName.FullName);
                else
                    target = new CodeObjectCreateExpression(typeName.FullName);

                var invoke = new CodeMethodInvokeExpression( // 関数呼び出し式
                    targetObject: target, // オブジェクト名: Console.
                    methodName: method.Name // メソッド名    : WriteLine
                );

                foreach (var p in method.GetParameters())
                {
                    invoke.Parameters.Add(new CodeArgumentReferenceExpression(p.Name));
                    var exp = new CodeParameterDeclarationExpression(p.ParameterType, p.Name);
                    mainMethod.Parameters.Add(exp);
                }

                if (method.ReturnType.Name != "Void")
                    mainMethod.Statements.Add(new CodeMethodReturnStatement(invoke));
                else
                    mainMethod.Statements.Add(invoke);

                mainClass.Members.Add(mainMethod);
            }

            return new PythonNetBinder();
        }

        public void Compile(string fileName)
        {
            if (fileName.EndsWith(".cs"))
            {
                ToCode(fileName);
                return;
            }

            // https://stackoverflow.com/questions/23551757/what-are-the-possible-parameters-for-compilerparameters-compileroptions
            var compileParameters = new CompilerParameters
            {
                OutputAssembly = fileName,
                CompilerOptions = "/optimize+ /target:library /unsafe"
            };
            var codeCompileUnit = new CodeCompileUnit();
#if UNITY
            codeCompileUnit.ReferencedAssemblies.Add("UnityEngine.dll");
            codeCompileUnit.ReferencedAssemblies.Add("UnityEditor.dll");
#elif GODOT
            codeCompileUnit.ReferencedAssemblies.Add("Godot.dll");
#elif INHOUSE
            codeCompileUnit.ReferencedAssemblies.Add("");
#endif
            codeCompileUnit.Namespaces.Add(nameSpace);

            var snippets = new CodeCompileUnit[] { };
            snippets.SetValue(codeCompileUnit, 0);

            // for (var i = 1; i < AppDomain.CreateDomain().GetAssemblies().Length; i++)
            // {
            //     var file = AppDomain.CreateDomain().GetAssemblies()[i];
            //     var fstring = System.IO.File.ReadAllText(file.Location);
            //     var snippet = new CodeSnippetCompileUnit();
            //     snippet.Value = fstring;
            //     snippets.SetValue(snippet, i);
            // }

            var result = CodeDomProvider.CreateProvider("C#")
                .CompileAssemblyFromDom(compileParameters, compilationUnits: snippets);

            foreach (var str in result.Output)
            {
                Console.WriteLine(str);
            }
        }

        private void ToCode(string fileName, string type = "C#")
        {
            var codeText = new StringBuilder();
            using (var codeWriter = new StringWriter(codeText))
            {
                var compilerOptions = new CodeGeneratorOptions
                {
                    IndentString = "    ", BracingStyle = type
                };
                CodeDomProvider.CreateProvider(type)
                    .GenerateCodeFromNamespace(nameSpace, codeWriter, compilerOptions);
            }

            using (var writer = new StreamWriter(fileName))
            {
                writer.Write(codeText);
            }
        }

#if UNITY_EDITOR
        [MenuItem("Assets/Generate Sample Script")]
        public static void GenerateSampleScript()
        {
            // アセットのパスを作成
            var filePath = "Assets/GenerateTest/Sample.cs";
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(filePath);
            EditorApplication.ExecuteMenuItem("");
            AssetDatabase.ImportAsset();
            AssetDatabase.Refresh();
        }
#endif

#if tool
        // TCPポートを開く
        
        // SDDebugger
#endif
    }
}