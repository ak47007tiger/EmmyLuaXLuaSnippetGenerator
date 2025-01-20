using UnityEditor;
using UnityEngine;
using System.Xml;
using System.IO;
using System;
using System.Linq;

namespace EmmyLuaSnippetGenerator
{
    [Serializable]
    public struct SettingOptions
    {
        public string GeneratePath;
        public string TargetNamespacesStr;
        public string GlobalVariablesStr;
        public bool GenerateCSAlias;
        public int SingleFileMaxLine;

        public static string SavePath => AppDomain.CurrentDomain.BaseDirectory + @"\EmmyLuaSnippetToolData\config.xml";

        public readonly string[] GetTargetNamespaces()
        {
            if (string.IsNullOrEmpty(TargetNamespacesStr))
            {
                return Array.Empty<string>();
            }

            return TargetNamespacesStr.Split(' ');
        }

        // varName, typeName
        public readonly (string, string)[] GetGlobalVariables()
        {
            if (string.IsNullOrEmpty(GlobalVariablesStr))
            {
                return Array.Empty<(string, string)>();
            }

            var varInfos = GlobalVariablesStr.Split(' ');
            return varInfos.Select(info => info.Split(':')).Select(info => (info[0], info[1])).ToArray();
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
        }

        private void OnGUI()
        {
            GUILayout.Label("配置文件将被保存在源代码文件的相同目录中.");

            GUILayout.Space(20);

            GUILayout.Label(
                "生成类型注解文件的路径"
                + "\n- 具体到目录, 不要指定文件名"
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

            GUILayout.Space(20);

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

            GUILayout.Label("生成不带CS.前缀的alias");
            _options.GenerateCSAlias = EditorGUILayout.Toggle(_options.GenerateCSAlias);
            
            GUILayout.Space(10);

            GUILayout.Label(
                "单个注解文件的最大行数"
                + "\n- 超过该行数时会自动拆分成多个文件"
                + "\n- 0表示不限制(不推荐)"
            );
            _options.SingleFileMaxLine = EditorGUILayout.IntField(
                _options.SingleFileMaxLine,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(20);

            if (GUILayout.Button("保存配置文件"))
            {
                XmlHelper.SaveConfig(_options, SettingOptions.SavePath);
                this.Close();
            }

            if (GUILayout.Button("取消"))
            {
                this.Close();
            }
        }
    }
}


