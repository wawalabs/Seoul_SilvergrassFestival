/*
	Copyright © Carl Emil Carlsen 2019-2023
	http://cec.dk
*/

#if ENABLE_MONO && !UNITY_2020
	#define DYNAMIC_METHOD_SUPPORTED
#endif

using System;
using System.Reflection;
#if DYNAMIC_METHOD_SUPPORTED
	using System.Reflection.Emit; // Not supported when compiling with IL2CPP.
#endif
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace OscSimpl.Internal
{
	/// <summary>
	/// An Entry holds a reference to one method on one targetObject. If the targetObject is a Component,
	/// then targetGameObject will also be set.
	/// </summary>
	[Serializable]
	public class OscMappingEntry
	{
		/// <summary>
		/// If target object is a Component, then this will hold the GameObject it is attatched to. Optional.
		/// </summary>
		public GameObject targetGameObject;

		/// <summary>
		/// The target object that will hold the target method.
		/// </summary>
		public Object serializedTargetObject;

		/// <summary>
		/// Alternatively to the above; a non-serialiazed runtime object that will hold the target method.
		/// </summary>
		public object nonSerializedTargetObject;

		/// <summary>
		/// Name of nonSerializedTargetObject. Used by Editor inspector.
		/// </summary>
		public string nonSerializedTargetObjectName;

		/// <summary>
		/// Name of target method. If the target method is a property, then that would be the set method.
		/// </summary>
		public string targetMethodName;

		/// <summary>
		/// Name of target field. Either targetMethodName or targetFieldName is used.
		/// </summary>
		public string targetFieldName;

		/// <summary>
		/// The type of the target data. For Impulse, Null and Empty OSCMessages this string is empty.
		/// </summary>
		[FormerlySerializedAs( "targetParamAssemblyQualifiedName" )] public string targetAssemblyQualifiedTypeName;

		/// <summary>
		/// Gets the target object.
		/// </summary>
		public object targetObject {
			get {
				if( serializedTargetObject != null ) return serializedTargetObject;
				return nonSerializedTargetObject;
			}
		}

		public OscMappingEntry() { }

		public OscMappingEntry( object targetObject, MethodInfo targetMethodInfo )
		{
			StoreTargetObjectReference( targetObject );
			
			Type targetParameterType = GetFirstParameterType( targetMethodInfo );
			if( targetParameterType != null ) targetAssemblyQualifiedTypeName = targetParameterType.AssemblyQualifiedName;

			targetMethodName =  targetMethodInfo.Name;
		}


		public OscMappingEntry( object targetObject, FieldInfo targetFieldInfo )
		{
			StoreTargetObjectReference( targetObject );
			
			Type targetParameterType = targetFieldInfo.FieldType;
			if( targetParameterType != null ) targetAssemblyQualifiedTypeName = targetParameterType.AssemblyQualifiedName;

			targetFieldName =  targetFieldInfo.Name;
		}


		void StoreTargetObjectReference( object targetObject )
		{
			if( targetObject == null ) return;

			if( targetObject is Object ) {
				serializedTargetObject = targetObject as Object;
				if( targetObject is Component ) targetGameObject = ( targetObject as Component ).gameObject;
			} else {
				nonSerializedTargetObject = targetObject;
				nonSerializedTargetObjectName = targetObject.GetType().Name;
			}
		}


		public bool TryCreateAction<T>( Type expectedDataType, out Action<T> action )
		{
			action = null;

			if( targetObject == null || ( string.IsNullOrEmpty( targetFieldName ) && string.IsNullOrEmpty( targetMethodName ) ) || string.IsNullOrEmpty( targetAssemblyQualifiedTypeName ) ) return false;

			// Recreate and test against parameter type.
			if( Type.GetType( targetAssemblyQualifiedTypeName ) != expectedDataType ) return false;

			try {
				if( !string.IsNullOrEmpty( targetFieldName ) ) {
					// Target is a field.

					FieldInfo fieldInfo = targetObject.GetType().GetField( targetFieldName );
#if DYNAMIC_METHOD_SUPPORTED
					// If we are compiling using MONO, then System.Reflection.Emit should be supported, and we can do this trick:
					// https://stackoverflow.com/a/16222886
					var setter = CreateSetter<object,T>( fieldInfo );
					action = ( T v ) => setter.Invoke( targetObject, v );
					return true;

#else
					// If we are compiling using IL2CPP then Dynamic System.Reflection.Emit is not supported. Instead we have to rely on reflection, which is far slower.
					// https://forum.unity.com/threads/notsupportedexception-with-log-detail.543644/#post-8186325
					action = ( T v ) => fieldInfo.SetValue( targetObject, v );
					return true;
#endif
				} else {
					// Target is a method.

					// NOTE: Encapsulating reflection calls in delegates should boost performance because security check is only
					// done once, first time the delegate is invoked.
					// https://www.c-sharpcorner.com/article/boosting-up-the-reflection-performance-in-c-sharp/
					action = Delegate.CreateDelegate( typeof( Action<T> ), targetObject, targetMethodName ) as Action<T>;
					return true;
				}
			} catch ( Exception e ) {
				Debug.Log( e );
				return false;
			}
		}


		public bool TryCreateAction( out Action action )
		{
			action = null;

			if( targetObject == null || string.IsNullOrEmpty( targetMethodName ) ) return false;

			try {
				action = Delegate.CreateDelegate( typeof( Action ), targetObject, targetMethodName ) as Action;
				return true;

			} catch {
				return false;
			}
		}


		public void ClearTargetMember()
		{
			targetMethodName = string.Empty;
			targetFieldName = string.Empty;
		}


#if DYNAMIC_METHOD_SUPPORTED
		static Action<S,T> CreateSetter<S,T>( FieldInfo field )
		{
			string methodName = field.ReflectedType.FullName + ".set_"+field.Name;
			var setterMethod = new DynamicMethod( methodName, returnType: null, new Type[ 2 ] { typeof( S ), typeof( T ) }, field.Module, skipVisibility: true );

			ILGenerator gen = setterMethod.GetILGenerator();
			if( field.IsStatic ) {
				gen.Emit( OpCodes.Ldarg_1 );
				gen.Emit( OpCodes.Stsfld, field );
			} else {
				gen.Emit( OpCodes.Ldarg_0 );
				gen.Emit( OpCodes.Ldarg_1 );
				gen.Emit( OpCodes.Stfld, field );
			}
			gen.Emit( OpCodes.Ret );
			return (Action<S,T>) setterMethod.CreateDelegate( typeof( Action<S,T> ) );
		}
#endif


		Type GetFirstParameterType( MethodInfo methodInfo )
		{
			ParameterInfo[] param = methodInfo.GetParameters();
			if( param.Length < 1 ) return null;
			return param[ 0 ].ParameterType;
		}
	}
}