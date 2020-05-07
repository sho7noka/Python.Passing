import clr

import sys
import os
import platform
import shutil
import subprocess

from ctypes import cdll
from distutils import sysconfig
from setuptools import Extension
from setuptools.command import build_ext


class PythonNetExtension(Extension):
    """
    python build system for csc

    >>> ext_modules=[PythonNetExtension('package_name', ["test.cs"])],
    """

    def __init__(self, name, sources=[], is_lib=True, compiler=None):
        """
        Args:
            name: 
            sourcedir: 
        """
        Extension.__init__(self, name, sources=sources)
        self.runtime = sysconfig.get_python_lib() + "Python.Runtime.dll"
        self.is_lib = is_lib
        self.compiler = compiler


class PythonNetBuild(build_ext.build_ext):
    """
    pythonnet 用 setuptoolの拡張
    Python.Runtime.dll と python.dll, python.zip
    https://docs.python.org/ja/3/distutils/apiref.html

    >>> cmdclass={'build_ext': PythonNetBuild},
    """

    if platform.platform() == "Windows":
        csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    else:
        csc = "csc"

    def run(self):
        try:
            _ = subprocess.check_output([self.csc, '/help'])
        except OSError:
            raise RuntimeError("csc must be installed to build the following extensions: " +
                               ", ".join(e.name for e in self.extensions))
        for ext in self.extensions:
            self.build_extension(ext)

    def build_extension(self, ext):
        """
        https://qiita.com/toshirot/items/dcf7809007730d835cfc
        Returns:
        """
        cmd = [
            ext.compiler or self.csc,
            "-nologo",
            "/t:library" if ext.is_lib else "/t:exe",
            "/out:%s.dll" % ext.name if ext.is_lib else "/out:%s.exe" % ext.name,
            "/r:%s" % ext.runtime,
            ext.sources,
        ]
        subprocess.check_call(cmd, shell=True)

        # package
        dllib = [".dll", ".a", ".so"]
        pythonz = sysconfig.get_python_lib(True, True)
        shutil.make_archive(pythonz, "zip", root_dir=pythonz)


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
    from System import Reflection as ref
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


def AddReferenceFrom(dll):
    """
    フルパスからlibを直接Reference
    戻値にモジュールを返す
    .NET AddReferenceToFileAndPath 互換

    Args:
        dll: マネージコードのパス

    Returns:
        object: 
        System, ManageCode モジュール

    >>> import clr_ext as clr
    >>> clr.AddReferenceFrom("")
    """

    clr.setPreload(True)
    clr.AddReference("System")
    import System

    if os.path.exists(dll):
        dir_name = os.path.dirname(dll)
        sys.path.append(dir_name)

    if dll.endswith(".dll"):
        asm = os.path.basename(dll).split(".")[0]
        clr.AddReference(asm)
        ref = System.Reflection.Assembly.LoadFile(dll)
        name_space = ref.GetTypes()[0].ToString().split(".")[0]

    try:
        Client = __import__(name_space)
    except ModuleNotFoundError:
        Client = None

    return System, Client


def AddReferenceUnManage(dll):
    """
    manage & unmanage 両用のリファレンス参照
    Args:
        dll: unmanage code

    Returns:

    >>> import clr_ext as clr
    >>> clr.AddReferenceUnmanage()
    """
    clr.setPreload(True)
    clr.AddReference("System")
    import System

    try:
        lib = cdll.LoadLibrary(dll)
        return System, lib
    except OSError:
        return AddReferenceFrom(dll)


# patch
if platform.platform() == "Windows":
    AddReference = AddReferenceUnManage
else:
    AddReference = AddReferenceFrom
