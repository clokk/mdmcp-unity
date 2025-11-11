using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;

namespace MCP
{
	public static class MCPUtils
	{
		public static void SetProperty(UnityEngine.Object targetObject, string propertyName, string propertyValueRaw)
		{
			var member = targetObject.GetType().GetMember(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault();

			if (member != null)
			{
				object valueToSet = null;
				Type memberType = null;

				if (member is PropertyInfo prop) memberType = prop.PropertyType;
				else if (member is FieldInfo field) memberType = field.FieldType;

				try
				{
					if (memberType == typeof(string)) valueToSet = propertyValueRaw;
					else if (memberType == typeof(int)) valueToSet = int.Parse(propertyValueRaw);
					else if (memberType == typeof(float)) valueToSet = float.Parse(propertyValueRaw, System.Globalization.CultureInfo.InvariantCulture);
					else if (memberType == typeof(bool)) valueToSet = bool.Parse(propertyValueRaw);
					else if (memberType.IsEnum) valueToSet = Enum.Parse(memberType, propertyValueRaw, true);
					else if (memberType == typeof(Vector2))
					{
						string[] parts = propertyValueRaw.Split(',');
						if (parts.Length == 2)
						{
							valueToSet = new Vector2(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
						}
					}
					else if (memberType == typeof(Vector3))
					{
						string[] parts = propertyValueRaw Split(',');
						if (parts.Length == 3)
						{
							valueToSet = new Vector3(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
						}
					}
					else if (memberType == typeof(Color))
					{
						string[] parts = propertyValueRaw.Split(',');
						if (parts.Length == 4)
						{
							valueToSet = new Color(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
						}
						else if (parts.Length == 3)
						{
							valueToSet = new Color(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"[MDMCP] Failed to parse property '{propertyName}' with value '{propertyValueRaw}'. Error: {ex.Message}");
					return;
				}

				if (valueToSet != null)
				{
					if (member is PropertyInfo pi && pi.CanWrite) pi.SetValue(targetObject, valueToSet);
					else if (member is FieldInfo fi) fi.SetValue(targetObject, valueToSet);
					Debug.Log($"[MDMCP] Set property '{propertyName}' on '{targetObject.GetType().Name}'.");
				}
				else
				{
					Debug.LogError($"[MDMCP] Unsupported or invalid value for property '{propertyName}' of type '{memberType}': {propertyValueRaw}");
				}
			}
			else
			{
				Debug.LogError($"[MDMCP] Could not find writable property or field '{propertyName}' on object '{targetObject.name}'.");
			}
		}
	}
}


