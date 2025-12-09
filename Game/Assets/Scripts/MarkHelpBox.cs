/*
 * author: mark joshwel
 * date: 16/1/2025
 * description: draws styled help boxes in the editor via the custom attribute drawer
 */
// https://discussions.unity.com/t/helpattribute-allows-you-to-use-helpbox-in-the-unity-inspector-window/659414/22

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Message severity levels supported by <see cref="HelpBoxAttribute" />.
/// </summary>
public enum HelpBoxMessageType
{
    None,
    Info,
    Warning,
    Error
}

/// <summary>
///     Attribute that renders a Unity help box above the annotated field.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class HelpBoxAttribute : PropertyAttribute
{
    /// <summary>
    ///     Severity that controls the icon and colour.
    /// </summary>
    public readonly HelpBoxMessageType MessageType;

    /// <summary>
    ///     Text displayed inside the help box.
    /// </summary>
    public readonly string Text;

    public HelpBoxAttribute(string text, HelpBoxMessageType messageType = HelpBoxMessageType.None)
    {
        Text = text;
        MessageType = messageType;
    }
}

/// <summary>
///     Custom drawer that renders <see cref="HelpBoxAttribute" /> instances in the inspector.
/// </summary>
[CustomPropertyDrawer(typeof(HelpBoxAttribute))]
public sealed class HelpBoxAttributeDrawer : DecoratorDrawer
{
    /// <inheritdoc />
    public override float GetHeight()
    {
        try
        {
            if (attribute is not HelpBoxAttribute helpBoxAttribute) return base.GetHeight();

            var helpBoxStyle = GUI.skin?.GetStyle("helpbox");
            if (helpBoxStyle == null) return base.GetHeight();

            var calculatedHeight = helpBoxStyle.CalcHeight(
                new GUIContent(helpBoxAttribute.Text),
                EditorGUIUtility.currentViewWidth);

            return Mathf.Max(40f, calculatedHeight + 4f);
        }
        catch (ArgumentException)
        {
            return 3f * EditorGUIUtility.singleLineHeight;
        }
    }

    /// <inheritdoc />
    public override void OnGUI(Rect position)
    {
        if (attribute is not HelpBoxAttribute helpBoxAttribute) return;

        EditorGUI.HelpBox(position, helpBoxAttribute.Text, GetMessageType(helpBoxAttribute.MessageType));
    }

    /// <summary>
    ///     Maps the custom enum to Unity’s native <see cref="MessageType" /> values.
    /// </summary>
    private static MessageType GetMessageType(HelpBoxMessageType helpBoxMessageType)
    {
        return helpBoxMessageType switch
        {
            HelpBoxMessageType.Info => MessageType.Info,
            HelpBoxMessageType.Warning => MessageType.Warning,
            HelpBoxMessageType.Error => MessageType.Error,
            _ => MessageType.None
        };
    }
}
#endif