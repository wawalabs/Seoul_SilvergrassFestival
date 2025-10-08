﻿/*
	Copyright © Carl Emil Carlsen 2019-2021
	http://cec.dk
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using OscSimpl.Internal;

namespace OscSimpl
{
	[CustomPropertyDrawer( typeof( OscMappingEntry ) )]
	public class OscMappingEntryDrawer : PropertyDrawer
	{
		const float verticalPadding = 2;

		static Dictionary<Type,Dictionary<Type,CandidateLists>> sharedMembersLookups;
		static char[] anonymousChars = new char[] { '<', '>' };


	public static float GetPropertyHeight()
		{
			return EditorGUIUtility.singleLineHeight + verticalPadding * 2;
		}


		public override float GetPropertyHeight( SerializedProperty entryProp, GUIContent label )
		{
			return GetPropertyHeight();
		}
		
		
		public override void OnGUI( Rect areaRect, SerializedProperty entryProp, GUIContent label )
		{
			// Begin.
			EditorGUI.BeginProperty( areaRect, label, entryProp );

			// Find property path to OscEvent in which this entry lives.
			String mappingPropPath = entryProp.propertyPath.Substring( 0, entryProp.propertyPath.IndexOf("_entries") -1 );

			// Get properties.
			SerializedProperty mappingProp = entryProp.serializedObject.FindProperty( mappingPropPath );
			SerializedProperty mappingTypeProp = mappingProp.FindPropertyRelative( "_type" );
			SerializedProperty targetGameObjectProp = entryProp.FindPropertyRelative( "targetGameObject" );
			SerializedProperty serializedTargetObjectProp = entryProp.FindPropertyRelative( "serializedTargetObject" );
			SerializedProperty nonSerializedTargetObjectNameProp = entryProp.FindPropertyRelative( "nonSerializedTargetObjectName" );
			SerializedProperty targetMethodNameProp = entryProp.FindPropertyRelative( "targetMethodName" );
			SerializedProperty targetFieldNameProp = entryProp.FindPropertyRelative( "targetFieldName" );
			SerializedProperty targetAssemblyTypeNameProp = entryProp.FindPropertyRelative( "targetAssemblyQualifiedTypeName" );

			//Debug.Log("oscEventParamAssemblyNameProp: " + oscEventParamAssemblyNameProp.name + " " + oscEventParamAssemblyNameProp.propertyPath + "  " + oscEventParamAssemblyNameProp.stringValue );

			// Get the parameter type.
			Type mappingParamType = OscMapping.OscMessagTypeToSystemType( (OscMessageType) mappingTypeProp.enumValueIndex );

			// Prepare positioning.
			Rect rect = areaRect;
			rect.height = EditorGUIUtility.singleLineHeight;
#if UNITY_2020_2_OR_NEWER
			rect.width = (areaRect.width+16) / 3f;
#else
			rect.width = (areaRect.width+16+26) / 3f;
#endif
			rect.y += verticalPadding;
			rect.x -= 16;

			// Target GameObject.
			bool displayGameObjectField = ( Application.isPlaying && targetGameObjectProp.objectReferenceValue != null ) || !Application.isPlaying;
			if( displayGameObjectField ) {
				targetGameObjectProp.objectReferenceValue = EditorGUI.ObjectField( rect, targetGameObjectProp.objectReferenceValue, typeof( GameObject ), true );
				if( targetGameObjectProp.objectReferenceValue is GameObject && !( serializedTargetObjectProp.objectReferenceValue is Component ) ) serializedTargetObjectProp.objectReferenceValue = null;
			} else {
				EditorGUI.LabelField( rect, string.Empty );
			}
#if UNITY_2020_2_OR_NEWER
			rect.x += rect.width + 2;
#else
			rect.x += rect.width - 13;
#endif

			// Target Object.
			rect.y += 1; // The default object selection GUI is taller than drop down.
			bool isAnonymous = targetMethodNameProp.stringValue.IndexOfAny( anonymousChars ) > -1;
			if( isAnonymous ) {
				EditorGUI.LabelField( rect, string.Empty );
			} else {
				bool displayObjectDropdown = targetGameObjectProp.objectReferenceValue != null;
				if( displayObjectDropdown ) {
					if( targetGameObjectProp.objectReferenceValue != null ) {
						GameObject go = targetGameObjectProp.objectReferenceValue as GameObject;
						Component[] components = go.GetComponents<Component>();
						int selectedComponentIndex = Array.IndexOf( components, serializedTargetObjectProp.objectReferenceValue as Component );
						GUIContent[] componentOptions = new GUIContent[components.Length];
						for( int i = 0; i < components.Length; i++ ) componentOptions[i] = new GUIContent( components[i].GetType().Name );
						int newSelectedComponentIndex = EditorGUI.Popup( rect, selectedComponentIndex, componentOptions );
						if( newSelectedComponentIndex != selectedComponentIndex ){
							serializedTargetObjectProp.objectReferenceValue = components[newSelectedComponentIndex];
							targetMethodNameProp.stringValue = string.Empty;
						}
					} else if( serializedTargetObjectProp.objectReferenceValue != null ) {
						EditorGUI.BeginDisabledGroup( true );
						serializedTargetObjectProp.objectReferenceValue = EditorGUI.ObjectField( rect, serializedTargetObjectProp.objectReferenceValue, typeof( Object ), true );
						EditorGUI.EndDisabledGroup();
					}
				} else if( !string.IsNullOrEmpty( nonSerializedTargetObjectNameProp.stringValue ) ) {
					// Non serialized object.
					EditorGUI.LabelField( rect, nonSerializedTargetObjectNameProp.stringValue );
				} else {
					EditorGUI.LabelField( rect, string.Empty );
				}
			}
#if UNITY_2020_2_OR_NEWER
			rect.x += rect.width + 1;
#else
			rect.x += rect.width - 13;
#endif

			// Member.
			if( isAnonymous ) {
				EditorGUI.LabelField( rect, "Anonymous" );
			} else {
				bool displayMemberDropdown = serializedTargetObjectProp.objectReferenceValue != null;
				if( displayMemberDropdown ) {
					bool isMappingParamNull = mappingParamType == null;
					CandidateLists candidateLists;
					GetMemberOptions( serializedTargetObjectProp.objectReferenceValue.GetType(), mappingParamType, out candidateLists );
					string targetMemberName = string.IsNullOrEmpty( targetFieldNameProp.stringValue ) ? targetMethodNameProp.stringValue : targetFieldNameProp.stringValue;
					int selectedMemberNameIndex = Array.IndexOf( candidateLists.memberNames, targetMemberName );
					bool isPrivateMethod = selectedMemberNameIndex == -1 && !string.IsNullOrEmpty( targetMemberName ) && Application.isPlaying;
					if( isPrivateMethod ) {
						EditorGUI.LabelField( rect, targetMemberName );
					} else {
						int newSelectedMemberNameIndex = EditorGUI.Popup( rect, selectedMemberNameIndex, candidateLists.options );
						if( newSelectedMemberNameIndex != selectedMemberNameIndex ){
							selectedMemberNameIndex = newSelectedMemberNameIndex;
							string selectedMemberName = candidateLists.memberNames[ selectedMemberNameIndex ];
							if( candidateLists.isFieldFlags[ selectedMemberNameIndex ] ){
								targetFieldNameProp.stringValue = selectedMemberName;
								targetMethodNameProp.stringValue = string.Empty;
							} else {
								targetMethodNameProp.stringValue = selectedMemberName;
								targetFieldNameProp.stringValue = string.Empty;
							}
							
							if( !isMappingParamNull ) targetAssemblyTypeNameProp.stringValue = mappingParamType.AssemblyQualifiedName;
						}
					}
				} else {
					EditorGUI.LabelField( rect, targetMethodNameProp.stringValue );
				}
			}

			// End.
			EditorGUI.EndProperty();
		}

		
		// All this stuff is just to make sure we only do the heavy reflection operation once per objecttype->paramtype.
		static void GetMemberOptions( Type objectType, Type mappingParamType, out CandidateLists candidateLists )
		{
			// Create object key dictionary.
			if( sharedMembersLookups == null ) sharedMembersLookups = new Dictionary<Type,Dictionary<Type,CandidateLists>>();

			// Get or create param key dictionary.
			Dictionary<Type,CandidateLists> paramLookup;
			if( !sharedMembersLookups.TryGetValue( objectType, out paramLookup ) ) {
				paramLookup = new Dictionary<Type,CandidateLists>();
				sharedMembersLookups.Add( objectType, paramLookup );
			}

			// Get or create method data.
			bool isParamNull = mappingParamType == null;
			if( isParamNull ) mappingParamType = typeof( NullType );
			if( paramLookup.TryGetValue( mappingParamType, out candidateLists ) ) return;

			// Create new candidate list data.
			candidateLists = new CandidateLists();
			List<string> memberNameList = new List<string>();
			List<GUIContent> optionList = new List<GUIContent>();
			List<bool> isFieldFlagList = new List<bool>();
			BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

			bool isPotentiallyWrappable = !isParamNull && !mappingParamType.IsPrimitive && !mappingParamType.IsValueType; // Only wrap when outlet param is object.

			FieldInfo[] fieldInfos = objectType.GetFields( flags );
			foreach( FieldInfo fieldInfo in fieldInfos ) {
				// Methods with one arguments.
				Type candidateParamType = fieldInfo.FieldType;
				if(
					candidateParamType == mappingParamType ||
					( isPotentiallyWrappable && candidateParamType.IsAssignableFrom( mappingParamType ) )
				) {
					string fieldName = fieldInfo.Name;
					optionList.Add( new GUIContent( fieldName ) );
					memberNameList.Add( fieldName );
					isFieldFlagList.Add( true );
				}
			}

			MethodInfo[] methodInfos = objectType.GetMethods( flags );
			foreach( MethodInfo methodInfo in methodInfos )
			{
				// Disregard methods that return somthing.
				if( methodInfo.ReturnType != typeof( void ) ) continue;

				ParameterInfo[] paramInfos = methodInfo.GetParameters();
				if( isParamNull && paramInfos.Length == 0 ) {
					// Methods without arguments.
					optionList.Add( new GUIContent( methodInfo.Name ) );
					memberNameList.Add( methodInfo.Name );
					isFieldFlagList.Add( false );
				} else if( paramInfos.Length == 1 ) {
					// Methods with one arguments.
					Type candidateParamType = paramInfos[0].ParameterType;
					if(
						candidateParamType == mappingParamType || 
						( isPotentiallyWrappable && candidateParamType.IsAssignableFrom( mappingParamType ) )
					){
						string prettyName = methodInfo.Name;
						if( prettyName.StartsWith( "set_" ) ) prettyName = prettyName.Substring( 4, prettyName.Length - 4 );
						optionList.Add( new GUIContent( prettyName ) );
						memberNameList.Add( methodInfo.Name );
						isFieldFlagList.Add( false );
					}
				}
			}

			// Store.
			candidateLists.memberNames = memberNameList.ToArray();
			candidateLists.options = optionList.ToArray();
			candidateLists.isFieldFlags = isFieldFlagList.ToArray();
			paramLookup.Add( mappingParamType, candidateLists );
		}


		class CandidateLists
		{
			// We need seperate arrays so that we can pass methodOptions to EditorGUI.Popup.
			public string[] memberNames;
			public GUIContent[] options;
			public bool[] isFieldFlags;
		}


		class NullType { }
	}
}