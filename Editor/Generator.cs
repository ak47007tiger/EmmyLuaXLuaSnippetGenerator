using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NetFile;
using OfficeOpenXml.FormulaParsing.ExpressionGraph.FunctionCompilers;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace EmmyLuaSnippetGenerator
{
    /// <summary>
    /// 该文件只用来给ide进行lua类型提示的,不要在运行时require该文件或者打包到版本中.
    /// </summary>
    public static class LuaTypeGenerator
    {
        private static SettingOptions _options;

        private static readonly HashSet<Type> luaNumberTypeSet = new HashSet<Type>
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double)
        };
        private static readonly HashSet<string> luaKeywordSet = new HashSet<string>
        {
            "and",
            "break",
            "do",
            "else",
            "elseif",
            "end",
            "false",
            "for",
            "function",
            "if",
            "in",
            "local",
            "nil",
            "not",
            "or",
            "repeat",
            "return",
            "then",
            "true",
            "until",
            "while"
        };

        public static readonly StringBuilder sb = new StringBuilder(1024);
        private static readonly StringBuilder tempSb = new StringBuilder(1024);
        private static readonly List<Type> exportTypeList = new List<Type>();

        private static readonly Dictionary<Type, List<MethodInfo>>
        extensionMethodsDic = new Dictionary<Type, List<MethodInfo>>();

        [MenuItem("LuaType/生成EmmyLua类型注解")]
        public static void GenerateEmmyTypeFiles()
        {
            if (!XmlHelper.TryLoadConfig(SettingOptions.SavePath, out SettingOptions loaded))
            {
                Debug.LogError("错误: 需要一份配置文件才能开始生成注解. 在[设置]页面中配置它然后保存!");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Lua Type", "确定按照当前的设置生成Lua类型注解?", "Yes", "No"))
            {
                return;
            }

            try
            {
                _options = loaded;

                var set = CollectAllExportType();
                exportTypeList.AddRange(set);

                HandleExtensionMethods();

                GenerateTypeDefines();

                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError("错误: " + e.Message);
                return;
            }

            Debug.Log("生成注解文件完毕.");
        }

        [MenuItem("LuaType/清除EmmyLua类型注解")]
        public static void ClearEmmyTypeFiles()
        {
            if (!Directory.Exists(_options.GeneratePath))
            {
                throw new Exception($"你指定的生成路径 {_options.GeneratePath} 不存在.");
            }

            int count = 0;
            string[] files = Directory.GetFiles(_options.GeneratePath, "TypeHint_*.lua");

            foreach (string file in files)
            {
                File.Delete(file);
                count++;
            }

            Debug.Log($"清除完毕, 删除了 {count} 份注解文件. (生成时会自动执行该清理)");
        }

        private static HashSet<Type> CollectAllExportType()
        {
            //收集要导出的类型
            var allAssembly = AppDomain.CurrentDomain.GetAssemblies();
            //去重复
            var set = new HashSet<Type>();
            foreach (var assemblyInst in allAssembly)
            {
                Type[] collection = CollectType(assemblyInst);
                foreach (var typeInst in collection)
                {
                    if (!set.Contains(typeInst))
                    {
                        set.Add(typeInst);
                    }
                }
            }
            return set;
        }

        public static bool IsExportType(Type item)
        {
            var targetNamespaces = _options.GetTargetNamespaces();

            for (int i = 0; i < targetNamespaces.Length; i++)
            {
                string itemNamespace = item.Namespace;
                if (string.IsNullOrEmpty(itemNamespace))
                {
                    if (item.FullName.Contains("Interop"))
                    {
                        return false;
                    }
                    //很多i,j,p,o这样的类占用空间浪费时间，类名少于3直接干掉
                    return item.Name.Length > 2;
                }
                else
                {
                    //不要编辑器
                    if (itemNamespace.StartsWith("UnityEditor"))
                    {
                        return false;
                    }
                    //lua里用不到
                    if (itemNamespace.Contains("Burst"))
                    {
                        return false;
                    }
                    if (itemNamespace.StartsWith(targetNamespaces[i]))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static Type[] CollectType(Assembly assembly)
        {
            var types = assembly.GetTypes();
            var retTypes = new HashSet<Type>();
            foreach (var item in types)
            {
                if (IsExportType(item) && !retTypes.Contains(item))
                {
                    retTypes.Add(item);
                }
            }
            return retTypes.ToArray();
        }

        private static void HandleExtensionMethods()
        {
            for (var i = 0; i < exportTypeList.Count; i++)
            {
                Type type = exportTypeList[i];

                MethodInfo[] publicStaticMethodInfos =
                    type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (var j = 0; j < publicStaticMethodInfos.Length; j++)
                {
                    MethodInfo methodInfo = publicStaticMethodInfos[j];
                    if (methodInfo.IsDefined(typeof(ExtensionAttribute), false))
                    {
                        Type extensionType = methodInfo.GetParameters()[0].ParameterType;
                        if (extensionMethodsDic.TryGetValue(extensionType, out List<MethodInfo> extensionMethodList))
                        {
                            extensionMethodList.Add(methodInfo);
                        }
                        else
                        {
                            List<MethodInfo> methodList = new List<MethodInfo> { methodInfo };
                            extensionMethodsDic.Add(extensionType, methodList);
                        }
                    }
                }
            }
        }

        private static void GenerateTypeDefines()
        {
            sb.Clear();
            sb.AppendLine("---@meta CSharp");
            sb.AppendLine("");
            sb.AppendLine("---@class NotExportType @表明该类型未导出");
            sb.AppendLine("");
            sb.AppendLine("---@class NotExportEnum @表明该枚举未导出");
            sb.AppendLine("");

            WriteGlobalVariablesDefine();

            WriteXLuaDefine();

            var targetNamespaces = _options.GetTargetNamespaces();

            for (int i = 0; i < targetNamespaces.Length; i++)
            {
                string namespaceName = targetNamespaces[i];

                string tableName = string.Format("CS.{0}", namespaceName);
                sb.AppendLine(string.Format("---@class {0}", tableName));
                sb.AppendLine(string.Format("{0} = {{}}", tableName));

                if (_options.GenerateCSAlias)
                {
                    sb.AppendLine(string.Format("---@alias {0} {1}", namespaceName, tableName));
                }

                sb.AppendLine("");
            }

            for (int i = 0; i < exportTypeList.Count; i++)
            {
                Type typeInst = exportTypeList[i];

                // 防止一些匿名类型的生成
                if (typeInst.ToString().Contains("<"))
                {
                    continue;
                }

                keepStringTypeName = typeInst == typeof(string);

                WriteClassDefine(typeInst);
                WriteClassFieldDefine(typeInst);
                sb.AppendLine(string.Format("{0} = {{}}", typeInst.ToLuaTypeName().ReplaceDotOrPlusWithUnderscore()));

                if (_options.GenerateCSAlias)
                {
                    WriteClassAliasDefine(typeInst);
                }

                WriteClassConstructorDefine(typeInst);
                WriteClassMethodDefine(typeInst);

                sb.AppendLine("");
            }

            WriteToFile();
        }

        #region TypeDefineFileGenerator

        public static void WriteToFile()
        {
            if (!Directory.Exists(_options.GeneratePath))
            {
                throw new Exception($"你指定的生成路径 {_options.GeneratePath} 不存在.");
            }

            ClearEmmyTypeFiles();

            string[] lines = sb.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            int fileCount = 0;
            int lineCount = 0;
            string fileName;

            StreamWriter writer = null;

            foreach (string line in lines)
            {
                if (writer == null)
                {
                    fileName = _options.GeneratePath + "TypeHint_" + fileCount + ".lua";
                    writer = new StreamWriter(fileName);
                    writer.WriteLine("---@meta");
                    writer.WriteLine("");
                }

                if (string.IsNullOrWhiteSpace(line) && _options.SingleFileMaxLine != 0 && lineCount >= _options.SingleFileMaxLine)
                {
                    writer?.Close();
                    fileCount++;
                    lineCount = 0;
                    writer = null;
                    continue;
                }

                writer.WriteLine(line);
                lineCount++; 
            }

            writer?.Close();
        }

        public static void WriteGlobalVariablesDefine()
        {
            foreach (var (varName, varTypeName) in _options.GetGlobalVariables())
            {
                sb.AppendLine(string.Format("---@type {0}", varTypeName));
                sb.AppendLine(string.Format("{0} = nil", varName));
                sb.AppendLine("");
            }
        }

        // xLua相关的定义单独写
        public static void WriteXLuaDefine()
        {
            // CS table
            sb.AppendLine(string.Format("---@class {0}", "CS"));
            sb.AppendLine("CS = {}");
            sb.AppendLine("");

            // typeof function
            sb.AppendLine(@"---@param obj any");
            sb.AppendLine(@"---@return CS.System.Type");
            sb.AppendLine(@"function typeof(obj) end");
            sb.AppendLine("");
        }

        public static void WriteClassDefine(Type type)
        {
            if (type.BaseType != null && !type.IsEnum)
            {
                sb.AppendLine(string.Format("---@class {0} : {1}", type.ToLuaTypeName(), type.BaseType.ToLuaTypeName()));
            }
            else
            {
                sb.AppendLine(string.Format("---@class {0}", type.ToLuaTypeName()));
            }
        }

        public static void WriteClassFieldDefine(Type classType)
        {
            FieldInfo[] publicInstanceFieldInfos =
                classType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            FieldInfo[] publicStaticFieldInfos =
                classType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            
            List<FieldInfo> fieldInfoList = new List<FieldInfo>();

            fieldInfoList.AddRange(publicStaticFieldInfos);

            if (!classType.IsEnum)
            {
                fieldInfoList.AddRange(publicInstanceFieldInfos);
            }

            for (int i = 0; i < fieldInfoList.Count; i++)
            {
                FieldInfo fieldInfo = fieldInfoList[i];

                if (fieldInfo.IsMemberObsolete(classType))
                {
                    continue;
                }

                string fieldTypeName = fieldInfo.FieldType.ToLuaTypeName();

                sb.AppendLine(string.Format("---@field {0} {1}", fieldInfo.Name, fieldTypeName));
            }

            PropertyInfo[] publicInstancePropertyInfo =
                classType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
            PropertyInfo[] publicStaticPropertyInfo =
                classType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            
            List<PropertyInfo> propertyInfoList = new List<PropertyInfo>();
            propertyInfoList.AddRange(publicStaticPropertyInfo);

            if (!classType.IsEnum)
            {
                propertyInfoList.AddRange(publicInstancePropertyInfo);
            }

            for (int i = 0; i < propertyInfoList.Count; i++)
            {
                PropertyInfo propertyInfo = propertyInfoList[i];
                if (propertyInfo.IsMemberObsolete(classType))
                {
                    continue;
                }

                Type propertyType = propertyInfo.PropertyType;
                sb.AppendLine(string.Format("---@field {0} {1}", propertyInfo.Name, propertyType.ToLuaTypeName()));
            }

            if (_options.InferGenericFieldType)
            {
                Dictionary<string, Type> inferedGenericFieldInfos = new();

                // 如果一个类型自身不是泛型, 但其父类是泛型, 则可认为在本次继承过程中完全地提供了父类所需的泛型信息.
                // 这种情况下, 从父类继承而来的泛型字段可以通过该信息进行类型推断.
                if (!classType.IsGenericType
                    && classType.BaseType != null
                    && classType.BaseType.IsGenericType
                // 只适用于父类的泛型参数只有一个的情况
                // 如果有多个泛型参数, 则不能很好的分析泛型字段应使用哪一个
                    && classType.BaseType.GetGenericArguments().Length == 1
                )
                {
                    var genericTypeDefinition = classType.BaseType.GetGenericTypeDefinition();

                    // 尝试推断泛型字段
                    var baseClassFields =
                            genericTypeDefinition.GetFields(BindingFlags.Public | BindingFlags.Instance)
                        .Concat(
                            genericTypeDefinition.GetFields(BindingFlags.Public | BindingFlags.Static)
                        )
                        .ToArray();

                    foreach (var field in baseClassFields)
                    {
                        if (!field.FieldType.IsGenericParameter)
                        {
                            continue;
                        }

                        inferedGenericFieldInfos.Add(field.Name, classType);
                    }

                    // 尝试推断泛型属性
                    var baseClassProperties =
                            genericTypeDefinition.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Concat(
                            genericTypeDefinition.GetProperties(BindingFlags.Public | BindingFlags.Static)
                        )
                        .ToArray();

                    foreach (var property in baseClassProperties)
                    {
                        if (!property.PropertyType.IsGenericParameter)
                        {
                            continue;
                        }

                        inferedGenericFieldInfos.Add(property.Name, classType);
                    }

                    // 写入
                    foreach (var inferedGenericFieldInfo in inferedGenericFieldInfos)
                    {
                        sb.AppendLine(string.Format(
                            "---@field {0} {1} -- infered from {2}",
                            inferedGenericFieldInfo.Key,
                            inferedGenericFieldInfo.Value.ToLuaTypeName(),
                            classType.BaseType
                        ));
                    }
                }
            }
        }

        public static void WriteClassAliasDefine(Type type)
        {
            sb.AppendLine(string.Format("---@alias {0} {1}", type.ToLuaTypeName(addCSPrefix: false), type.ToLuaTypeName(addCSPrefix: true)));
            sb.AppendLine("");
        }

        public static void WriteClassConstructorDefine(Type type)
        {
            if (type == typeof(MonoBehaviour) || type.IsSubclassOf(typeof(MonoBehaviour)))
            {
                return;
            }

            string className = type.ToLuaTypeName().ReplaceDotOrPlusWithUnderscore();
            ConstructorInfo[] constructorInfos = type.GetConstructors();
            if (constructorInfos.Length == 0)
            {
                return;
            }

            for (int i = 0; i < constructorInfos.Length - 1; i++)
            {
                ConstructorInfo ctorInfo = constructorInfos[i];
                if (ctorInfo.IsStatic || ctorInfo.IsGenericMethod)
                {
                    continue;
                }

                WriteOverloadMethodCommentDecalre(
                    parameterInfos: ctorInfo.GetParameters(), 
                    returnType: type, 
                    classType: null // constructor has no "class type", although it's a member of the class
                );
            }

            ConstructorInfo lastCtorInfo = constructorInfos[constructorInfos.Length - 1];
            WriteMethodFunctionDeclare(lastCtorInfo.GetParameters(), type, "New", className, true);
        }

        public static void WriteClassMethodDefine(Type type)
        {
            // string classNameWithNameSpace = type.ToLuaTypeName().Replace(".", "_");
            string classNameWithNameSpace = type.ToLuaTypeName();

            Dictionary<string, List<MethodInfo>> methodGroup = new Dictionary<string, List<MethodInfo>>();
            MethodInfo[] publicInstanceMethodInfos =
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            MethodInfo[] publicStaticMethodInfos =
                type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Action<MethodInfo> recordMethodGroup = methodInfo =>
            {
                string methodName = methodInfo.Name;

                if (methodInfo.IsGenericMethod)
                {
                    return;
                }

                if (methodName.StartsWith("get_") || methodName.StartsWith("set_") || methodName.StartsWith("op_"))
                {
                    return;
                }

                if (methodName.StartsWith("add_") || methodName.StartsWith("remove_"))
                {
                    return;
                }

                if (methodGroup.ContainsKey(methodName))
                {
                    List<MethodInfo> methodInfoList = methodGroup[methodName];
                    if (methodInfoList == null)
                    {
                        methodInfoList = new List<MethodInfo>();
                    }

                    methodInfoList.Add(methodInfo);
                    methodGroup[methodName] = methodInfoList;
                }
                else
                {
                    methodGroup.Add(methodName, new List<MethodInfo> { methodInfo });
                }
            };

            for (int i = 0; i < publicStaticMethodInfos.Length; i++)
            {
                MethodInfo methodInfo = publicStaticMethodInfos[i];
                if (methodInfo.IsMemberObsolete(type))
                {
                    continue;
                }

                recordMethodGroup(methodInfo);
            }

            for (int i = 0; i < publicInstanceMethodInfos.Length; i++)
            {
                MethodInfo methodInfo = publicInstanceMethodInfos[i];
                if (methodInfo.IsMemberObsolete(type))
                {
                    continue;
                }

                recordMethodGroup(methodInfo);
            }

            foreach (var oneGroup in methodGroup)
            {
                List<MethodInfo> methodInfoList = oneGroup.Value;
                //前面的方法都是overload
                for (int i = 0; i < methodInfoList.Count - 1; i++)
                {
                    var methodInfo = methodInfoList[i];

                    WriteOverloadMethodCommentDecalre(
                        parameterInfos: methodInfo.GetParameters(), 
                        returnType: methodInfo.ReturnType,
                        classType: methodInfo.IsStatic ? null : type
                    );
                }

                MethodInfo lastMethodInfo = methodInfoList[methodInfoList.Count - 1];
                WriteMethodFunctionDeclare(lastMethodInfo.GetParameters(), lastMethodInfo.ReturnType,
                    lastMethodInfo.Name,
                    classNameWithNameSpace, lastMethodInfo.IsStatic);
            }

            WriteExtensionMethodFunctionDecalre(type);
        }

        public static void WriteOverloadMethodCommentDecalre(
            ParameterInfo[] parameterInfos,
            Type returnType,
            Type classType // if null, means static method
        )
        {
            List<ParameterInfo> outOrRefParameterInfoList = new List<ParameterInfo>();

            tempSb.Clear();
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                ParameterInfo parameterInfo = parameterInfos[i];
                string parameterName = parameterInfo.Name;
                string parameterTypeName = parameterInfo.ParameterType.ToLuaTypeName();
                if (parameterInfo.IsOut)
                {
                    parameterName = "out_" + parameterName;
                    outOrRefParameterInfoList.Add(parameterInfo);

                    parameterTypeName = parameterInfo.ParameterType.GetElementType().ToLuaTypeName();
                }
                else if (parameterInfo.ParameterType.IsByRef)
                {
                    parameterName = "ref_" + parameterName;
                    outOrRefParameterInfoList.Add(parameterInfo);

                    parameterTypeName = parameterInfo.ParameterType.GetElementType().ToLuaTypeName();
                }

                // write self parameter
                if (i == 0 && classType != null)
                {
                    string selfParameterName = "self";
                    string selfParameterTypeName = classType.ToLuaTypeName();
                    tempSb.Append(string.Format("{0}: {1}, ", selfParameterName, selfParameterTypeName));
                }

                // write other parameters
                parameterName = EscapeLuaKeyword(parameterName);
                if (i == parameterInfos.Length - 1)
                {
                    tempSb.Append(string.Format("{0}: {1}", parameterName, parameterTypeName));
                }
                else
                {
                    tempSb.Append(string.Format("{0}: {1}, ", parameterName, parameterTypeName));
                }
            }

            //return
            List<Type> returnTypeList = new List<Type>();
            if (returnType != null && returnType != typeof(void))
            {
                returnTypeList.Add(returnType);
            }

            for (int i = 0; i < outOrRefParameterInfoList.Count; i++)
            {
                returnTypeList.Add(outOrRefParameterInfoList[i].ParameterType.GetElementType());
            }

            string returnTypeString = "";
            for (int i = 0; i < returnTypeList.Count; i++)
            {
                if (i == returnTypeList.Count - 1)
                {
                    returnTypeString += returnTypeList[i].ToLuaTypeName();
                }
                else
                {
                    returnTypeString += returnTypeList[i].ToLuaTypeName() + ", ";
                }
            }

            if (returnTypeList.Count > 0)
            {
                sb.AppendLine(string.Format("---@overload fun({0}) : {1}", tempSb, returnTypeString));
            }
            else
            {
                sb.AppendLine(string.Format("---@overload fun({0})", tempSb));
            }
        }

        public static void WriteMethodFunctionDeclare(ParameterInfo[] parameterInfos, Type returnType,
            string methodName,
            string className, bool isStaticMethod)
        {
            List<ParameterInfo> outOrRefParameterInfoList = new List<ParameterInfo>();

            tempSb.Clear();
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                ParameterInfo parameterInfo = parameterInfos[i];
                string parameterName = parameterInfo.Name;
                string parameterTypeName = parameterInfo.ParameterType.ToLuaTypeName();
                if (parameterInfo.IsOut)
                {
                    parameterName = "out_" + parameterName;
                    outOrRefParameterInfoList.Add(parameterInfo);

                    parameterTypeName = parameterInfo.ParameterType.GetElementType().ToLuaTypeName();
                }
                else if (parameterInfo.ParameterType.IsByRef)
                {
                    parameterName = "ref_" + parameterName;
                    outOrRefParameterInfoList.Add(parameterInfo);

                    parameterTypeName = parameterInfo.ParameterType.GetElementType().ToLuaTypeName();
                }

                parameterName = EscapeLuaKeyword(parameterName);

                if (i == parameterInfos.Length - 1)
                {
                    tempSb.Append(parameterName);
                }
                else
                {
                    tempSb.Append(string.Format("{0}, ", parameterName));
                }

                sb.AppendLine(string.Format("---@param {0} {1}", parameterName, parameterTypeName));
            }

            //return
            bool haveReturen = returnType != null && returnType != typeof(void) || outOrRefParameterInfoList.Count > 0;

            if (haveReturen)
            {
                sb.Append("---@return ");
            }

            if (returnType != null && returnType != typeof(void))
            {
                sb.Append(returnType.ToLuaTypeName());
            }

            for (int i = 0; i < outOrRefParameterInfoList.Count; i++)
            {
                sb.Append(string.Format(",{0}",
                    outOrRefParameterInfoList[i].ParameterType.GetElementType().ToLuaTypeName()));
            }

            if (haveReturen)
            {
                sb.AppendLine("");
            }

            if (isStaticMethod)
            {
                sb.AppendLine(string.Format("function {0}.{1}({2}) end", className, methodName, tempSb));
            }
            else
            {
                sb.AppendLine(string.Format("function {0}:{1}({2}) end", className, methodName, tempSb));
            }
        }

        private static void WriteExtensionMethodFunctionDecalre(Type type)
        {
            if (extensionMethodsDic.TryGetValue(type, out List<MethodInfo> extensionMethodList))
            {
                for (var i = 0; i < extensionMethodList.Count; i++)
                {
                    MethodInfo methodInfo = extensionMethodList[i];
                    ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                    if (parameterInfos.Length > 0)
                    {
                        //第一个param是拓展类型，去掉
                        parameterInfos = parameterInfos.ToList().GetRange(1, parameterInfos.Length - 1).ToArray();
                    }

                    Type returnType = methodInfo.ReturnType;
                    string methodName = methodInfo.Name;
                    string classNameWithNameSpace = type.ToLuaTypeName();

                    WriteMethodFunctionDeclare(parameterInfos, returnType, methodName, classNameWithNameSpace, false);
                }
            }
        }

        #endregion

        private static bool TypeIsExport(Type type)
        {
            return exportTypeList.Contains(type) || type == typeof(string) ||
                               luaNumberTypeSet.Contains(type) || type == typeof(bool);
        }

        private static bool keepStringTypeName;

        public static string ToLuaTypeName(this Type type, bool addCSPrefix = true)
        {
            string prefix = addCSPrefix ? "CS." : "";

            if (type == null)
            {
                return "NullType";
            }

            if (luaNumberTypeSet.Contains(type))
            {
                return "number";
            }

            if (type == typeof(string))
            {
                return keepStringTypeName ? "System.String" : "string";
            }

            if (type == typeof(bool))
            {
                return "boolean";
            }

            string typeName = type.FullName;
            if (typeName == null)
            {
                return prefix + type.ToString().EscapeGenericTypeSuffix();
            }

            if (type.IsEnum)
            {
                return prefix + type.FullName.EscapeGenericTypeSuffix().Replace("+", ".");
            }

            //去除泛型后缀
            typeName = typeName.EscapeGenericTypeSuffix();

            int bracketIndex = typeName.IndexOf("[[");
            if (bracketIndex > 0)
            {
                typeName = typeName.Substring(0, bracketIndex);
            }

            return prefix + typeName;
        }

        private static Dictionary<Type, string> CSharpTypeNameDic = new Dictionary<Type, string>
        {
            {typeof(byte), "byte"},
            {typeof(sbyte), "sbyte"},
            {typeof(short), "short"},
            {typeof(ushort), "ushort"},
            {typeof(int), "int"},
            {typeof(uint), "uint"},
            {typeof(long), "long"},
            {typeof(ulong), "ulong"},
            {typeof(float), "float"},
            {typeof(double), "double"},
            {typeof(bool), "bool"},
            {typeof(string), "string"},
        };


        public static string EscapeLuaKeyword(string s)
        {
            if (luaKeywordSet.Contains(s))
            {
                return "_" + s;
            }

            return s;
        }

        public static string ReplaceDotOrPlusWithUnderscore(this string s)
        {
            // return s.Replace(".", "_").Replace("+", "_");
            return s.Replace("+", "_");
        }

        public static string EscapeGenericTypeSuffix(this string s)
        {
            string result = Regex.Replace(s , @"\`[0-9]+", "");

            result = result.Replace("+", ".");

            return result;
        }

        public static bool IsMemberObsolete(this MemberInfo memberInfo, Type type)
        {
            return memberInfo.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length > 0 ||
                   IsMemberFilter(memberInfo, type);
        }

        public static bool IsMemberFilter(MemberInfo mi, Type type)
        {
            if (type.IsGenericType)
            {
            }
            return false;
        }
    }
}