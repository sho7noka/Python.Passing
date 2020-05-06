﻿using System;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.CSharp;
using Microsoft.CodeAnalysis;
#if UNITY
using UnityEngine;
using UnityEditor;
#elif GODOT
using Godot;
#endif
using Python.Runtime;
using File = Godot.File;


namespace Python.Passing
{
    /**
     * Interpreter. pipenv pyenv $ conda
     * TODO: インタープリター位置が指定できないと実行時にコケる
     */
    public class Interpreter
    {
        /**
         * https://github.com/pythonnet/pythonnet/wiki/Using-Python.NET-with-Virtual-Environments
         */
        public static void PyConsole()
        {
            var pathToVirtualEnv = @"path\to\env";

            // Environment.SetEnvironmentVariable("PATH", pathToVirtualEnv, EnvironmentVariableTarget.Process);
            // Environment.SetEnvironmentVariable("PYTHONHOME", pathToVirtualEnv, EnvironmentVariableTarget.Process);
            // Environment.SetEnvironmentVariable("PYTHONPATH", $"{pathToVirtualEnv}\\Lib\\site-packages;{pythonPath}\\Lib", EnvironmentVariableTarget.Process);
            //
            // PythonEngine.PythonHome = pathToVirtualEnv;
            // PythonEngine.PythonPath = Environment.GetEnvironmentVariable("PYTHONPATH", EnvironmentVariableTarget.Process);
            // pth か py を生成


            // Console.Write(new DLLTest.MyUtilities().GetValue());
            CsBinder.Generate(typeof(DLLTest.MyUtilities)).ToCode("Client.cs");
            CsBinder.Generate(typeof(DLLTest.MyUtilities)).Compile("Client.dll");

            using (Py.GIL())
            {
                dynamic py = new PyExpandoObject();
                Console.Write(py.sys.version_info);
            }
        }
    }

    /**
     * <summary>
     * like Dynamic Member Lookup type for Swift
     * </summary>
     *
     * <code>
     * py = new PyExpandoObject();
     * py.sys.version_info
     * </code>
     *
     * <code>
     * py = new PyExpandoObject();
     * py.my_value = true;
     * </code>
     * 
     * TODO: dynamic import&intelisense
     */
    class PyExpandoObject : DynamicObject
    {
        private Dictionary<string, PyObject> sysModule;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            try
            {
                dynamic module = Py.Import(binder.Name);
                result = module;
                return true;
            }
            catch (PythonException e)
            {
                // pip install binder.Name
                throw;
            }

            return false;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            // オブジェクトはスコープ内で変換
            if (value.GetType().IsClass)
            {
                using (var scope = Py.CreateScope())
                {
                    // convert the binder object to a PyObject
                    var pyPerson = binder.ToPython();

                    // create a Python variable "person"
                    scope.Set("person", pyPerson);

                    // the person object may now be used in Python
                    var code = "fullName = person.FirstName + ' ' + person.LastName";
                    scope.Exec(code);
                }
            }

            // プリミティブはそのまま戻す
            else
            {
                binder.Name.ToPython().SetAttr(binder.Name, value.ToPython());
            }

            return true;
        }
    }

    public class CsBinder
    {
        static CodeNamespace nameSpace;

        /**
         * 指定namespace以下のpublicを取得
         * https://mitosuya.net/execute-all-class-in-namespace
         * TODO: レガシー
         */
        public static CsBinder Generate(Type typeName)
        {
            nameSpace = new CodeNamespace(name: "Client");
            var comment = new CodeCommentStatement(new CodeComment("this code is generated from C#. Do not Edit."));
            nameSpace.Comments.Add(comment);

            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "System"));
#if UNITY
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "UnityEngine"));
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "UnityEditor"));
#elif GODOT
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: "Godot"));
#elif INHOUSE
            nameSpace.Imports.Add(new CodeNamespaceImport(nameSpace: ""));
#endif

            // class Runtime {
            var mainClass = new CodeTypeDeclaration(name: "Runtime");
            // mainClass.BaseTypes.Add(new CodeTypeReference(typeof(MonoBehaviour)));

            var fields = typeName.GetFields(BindingFlags.GetField | BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var variable = new CodeMemberField(field.FieldType.Name, field.Name);

                // Attribute
                var codeAttrDecl = new CodeAttributeDeclaration(
                    "SerializeField",
                    new CodeAttributeArgument(new CodePrimitiveExpression(false)));
                variable.CustomAttributes.Add(codeAttrDecl);

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
                mainMethod.Comments.Add(new CodeCommentStatement("doc comment"));

                CodeExpression target;
                if ((method.Attributes & MethodAttributes.Static) != 0)
                {
                    target = new CodeSnippetExpression(typeName.FullName);
                }
                else
                {
                    target = new CodeObjectCreateExpression(typeName.FullName);
                }

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

            return new CsBinder();
        }

        public static readonly string CODE_TEMPLATE = @"
public class #CLASS_NAME#
{
  public void #METHOD_NAME#() {

  }
}
";

        [MenuItem("Assets/Generate Sample Script")]
        private static void GenerateSampleScript()
        {
            var filePath = "Assets/GenerateTest/Sample.cs";
            var className = "SampleClass";
            var methodName = "SampleMethod";

            // アセットのパスを作成
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(filePath);

            // コードテンプレートを置換
            var code = CODE_TEMPLATE.Replace(@"#CLASS_NAME#", className).Replace(@"#METHOD_NAME#", methodName);

            File.WriteAllText(assetPath, code);
            AssetDatabase.Refresh();
        }

        public void ToCode(string fileName, string type = "C#")
        {
            var codeText = new StringBuilder();
            using (var codeWriter = new StringWriter(codeText))
            {
                var compilerOptions = new CodeGeneratorOptions {IndentString = "    ", BracingStyle = type};
                CodeDomProvider.CreateProvider(type)
                    .GenerateCodeFromNamespace(nameSpace, codeWriter, compilerOptions);
            }

            using (var writer = new StreamWriter(fileName))
            {
                writer.Write(codeText);
            }
        }

        public void Compile(string fileName)
        {
            // https://stackoverflow.com/questions/23551757/what-are-the-possible-parameters-for-compilerparameters-compileroptions
            var compileParameters = new CompilerParameters
            {
                OutputAssembly = fileName, CompilerOptions = "/optimize+ /target:library /unsafe"
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
            
            for (var i = 1; i < AppDomain.CreateDomain().GetAssemblies().Length; i++)
            {
                var file = AppDomain.CreateDomain().GetAssemblies()[i];
                var fstring = System.IO.File.ReadAllText(file.Location);
                var snippet = new CodeSnippetCompileUnit();
                snippet.Value = fstring;
                snippets.SetValue(snippet, i);    
            }

            var result = CodeDomProvider.CreateProvider("C#")
                .CompileAssemblyFromDom(compileParameters, compilationUnits: snippets);

            foreach (var str in result.Output)
            {
                Console.WriteLine(str);
            }
        }
    }
}