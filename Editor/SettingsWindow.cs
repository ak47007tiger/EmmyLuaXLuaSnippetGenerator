using UnityEditor;
using UnityEngine;
using System.Xml;
using System.IO;
using System;

namespace EmmyLuaSnippetGenerator
{
    [Serializable]
    public struct SettingOptions
    {
        public string GeneratePath;
        public string TargetNamespacesStr;
        public bool GenerateCSAlias;

        public static string SavePath => AppDomain.CurrentDomain.BaseDirectory + @"\EmmyLuaSnippetToolData\config.xml";

        public readonly string[] GetTargetNamespaces()
        {
            return TargetNamespacesStr.Split(' ');
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

            GUILayout.Label("生成类型注解文件的路\n(具体到文件名)");
            _options.GeneratePath = EditorGUILayout.TextField(
                _options.GeneratePath,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(10);

            GUILayout.Label("要生成注解的C#命名空间\n(多个命名空间用空格分隔)");
            _options.TargetNamespacesStr = EditorGUILayout.TextField(
                _options.TargetNamespacesStr,
                GUILayout.MinWidth(200)
            );

            GUILayout.Space(10);

            GUILayout.Label("生成不带CS.前缀的alias");
            _options.GenerateCSAlias = EditorGUILayout.Toggle(_options.GenerateCSAlias);
            
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


