using UnityEditor;
using UnityEngine;
using System.Xml;
using System.IO;
using System;
using System.Linq;

namespace EmmyLuaSnippetGenerator
{
    [Serializable]
    public sealed class SettingOptions
    {
        public string GeneratePath;
        public string TargetNamespacesStr;
        public string GlobalVariablesStr;
        public string FunctionCompatibleTypesStr;
        public bool GenerateCSAlias = true;
        public bool InferGenericFieldType = true;
        public int SingleFileMaxLine = 5000;

        private static string _saveRootPath = null;
        public static string SaveRootPath
        {
            get => string.IsNullOrWhiteSpace(_saveRootPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : _saveRootPath;
            set => _saveRootPath = value;
        }
        public static string SavePath => Path.Combine(SaveRootPath, @"EmmyLuaSnippetToolData\config.xml");

        public string[] GetTargetNamespaces()
        {
            if (string.IsNullOrWhiteSpace(TargetNamespacesStr))
            {
                return Array.Empty<string>();
            }

            return TargetNamespacesStr.Split(' ');
        }

        // varName, typeName
        public (string, string)[] GetGlobalVariables()
        {
            if (string.IsNullOrWhiteSpace(GlobalVariablesStr))
            {
                return Array.Empty<(string, string)>();
            }

            var varInfos = GlobalVariablesStr.Split(' ');
            return varInfos.Select(info => info.Split(':')).Select(info => (info[0], info[1])).ToArray();
        }

        public string[] GetFunctionCompatibleTypes()
        {
            if (string.IsNullOrWhiteSpace(FunctionCompatibleTypesStr))
            {
                return Array.Empty<string>();
            }

            return FunctionCompatibleTypesStr.Split(' ');
        }
    }

    public sealed class SettingsWindow : EditorWindow
    {
        private SettingOptions _options;

        [MenuItem("LuaType/设置")]
        public static void ShowWindow()
        {
            GetWindow<SettingsWindow>("Lua类型注解文件设置");
        }

        private void OnEnable()
        {
            if (XmlHelper.TryLoadConfig(SettingOptions.SavePath, out SettingOptions settings))
            {
                _options = settings;
            }
            else
            {
                _options = new();
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(20);

            GUILayout.Label(
                "配置文件的存放路径"
                + "\n- 提供绝对目录, 不要指定文件名"
            );
            SettingOptions.SaveRootPath = EditorGUILayout.TextField(
                SettingOptions.SaveRootPath,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(10);

            GUILayout.Label(
                "生成类型注解文件的路径"
                + "\n- 提供以\\结尾的绝对目录, 不要指定文件名"
            );
            _options.GeneratePath = EditorGUILayout.TextField(
                _options.GeneratePath,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(10);

            GUILayout.Label(
                "要生成注解的C#命名空间"
                + "\n- 多个命名空间用空格分隔"
                + "\n- 例如: UnityEngine DG FairyGUI"
            );
            _options.TargetNamespacesStr = EditorGUILayout.TextField(
                _options.TargetNamespacesStr,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(10);

            GUILayout.Label(
                "要生成注解的全局变量"
                + "\n- 变量名:类型名, 多个组用空格分隔"
                + "\n- 例如: UNITY_EDITOR:boolean DEBUG_LV:integer"
            );
            _options.GlobalVariablesStr = EditorGUILayout.TextField(
                _options.GlobalVariablesStr,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(10);

            GUILayout.Label(
                "使以下类型名兼容Lua function类型"
                + "\n- 多个类型名用空格分隔"
                + "\n- 例如: System.Action FairyGUI.EventCallback0"
            );
            _options.FunctionCompatibleTypesStr = EditorGUILayout.TextField(
                _options.FunctionCompatibleTypesStr,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(10);

            GUILayout.Label(
                "生成带CS.前缀的兼容alias"
                + "\n- 启用后, 将为生成的类型名额外添加带CS.前缀的版本"
            );
            _options.GenerateCSAlias = EditorGUILayout.Toggle(_options.GenerateCSAlias);
            
            GUILayout.Space(10);

            GUILayout.Label(
                "尝试推理泛型字段类型"
                + "\n- 启用后, 将在继承泛型类的非泛型派生中添加泛型字段的类型"
                + "\n- 显著影响注解生成速度, 但不影响类型分析性能"
            );
            _options.InferGenericFieldType = EditorGUILayout.Toggle(_options.InferGenericFieldType);

            GUILayout.Space(10);

            GUILayout.Label(
                "单个注解文件的最大行数"
                + "\n- 超过该行数时会自动拆分成多个文件"
                + "\n- 大幅影响类型分析性能, 请依据电脑配置设置"
            );
            _options.SingleFileMaxLine = (int)EditorGUILayout.Slider(
                _options.SingleFileMaxLine,
                5000,
                40000,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(20);

            if (GUILayout.Button("保存配置文件"))
            {
                try
                {
                    XmlHelper.SaveConfig(_options, SettingOptions.SavePath);
                    this.Close();
                }
                catch (UnauthorizedAccessException e)
                {
                    Debug.LogError($"错误: 没有对目录 {SettingOptions.SaveRootPath} 的操作权限. 尝试修改配置文件的存放路径.\n{e.StackTrace}");
                }
            }

            if (GUILayout.Button("取消"))
            {
                this.Close();
            }
        }
    }
}


