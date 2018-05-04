using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Type = System.Type;
using Convert = System.Convert;
using System.Linq;
using UnityObject = UnityEngine.Object;


namespace UnityEditor.VFX.UI
{
    class FloatNAffector : IFloatNAffector<float>, IFloatNAffector<Vector2>, IFloatNAffector<Vector3>, IFloatNAffector<Vector4>
    {
        float IFloatNAffector<float>.GetValue(object floatN)
        {
            return (FloatN)floatN;
        }
        Vector2 IFloatNAffector<Vector2>.GetValue(object floatN)
        {
            return (FloatN)floatN;
        }
        Vector3 IFloatNAffector<Vector3>.GetValue(object floatN)
        {
            return (FloatN)floatN;
        }
        Vector4 IFloatNAffector<Vector4>.GetValue(object floatN)
        {
            return (FloatN)floatN;
        }

        public static FloatNAffector Default = new FloatNAffector();
    }

    static class VFXConverter
    {
        public static bool CanConvert(Type type)
        {
            return type == typeof(Color) ||
                type == typeof(Vector4) ||
                type == typeof(Vector3) ||
                type == typeof(Position) ||
                type == typeof(Vector) ||
                type == typeof(Vector2) ||
                type == typeof(float);
        }

        public static T ConvertTo<T>(object value)
        {
            return (T)ConvertTo(value, typeof(T));
        }


        static readonly Dictionary<System.Type,Dictionary<System.Type, System.Func<object,object> >> s_Converters = new Dictionary<System.Type,Dictionary<System.Type, System.Func<object,object> >>();

        static VFXConverter()
        {
            //Register conversion that you only want in the UI here
            RegisterCustomConverter<Vector3,Vector4>(t=>new Vector4(t.x,t.y,t.z));
            RegisterCustomConverter<Vector2,Vector4>(t=>new Vector4(t.x,t.y,0));
            RegisterCustomConverter<Vector2,Vector3>(t=>new Vector3(t.x,t.y,0));
            RegisterCustomConverter<Vector2,Color>(t=>new Color(t.x,t.y,0));
            RegisterCustomConverter<Vector3,Color>(t=>new Color(t.x,t.y,t.z));
            RegisterCustomConverter<Vector4,Color>(t=>new Color(t.x,t.y,t.z,t.w));
            RegisterCustomConverter<Matrix4x4,Transform>(MakeTransformFromMatrix4x4);
            RegisterCustomConverter<Vector2,float>(t=>t.x);
            RegisterCustomConverter<Vector3,float>(t=>t.x);
            RegisterCustomConverter<Vector4,float>(t=>t.x);
            RegisterCustomConverter<Color,Vector2>(t=>new Vector2(t.r,t.g));
            RegisterCustomConverter<Color,Vector3>(t=>new Vector3(t.r,t.g,t.b));
            RegisterCustomConverter<Color,float>(t=>t.a);
        }


        static Transform MakeTransformFromMatrix4x4(Matrix4x4 mat)
        {
            var result = new Transform
            {
                position = mat.MultiplyPoint(Vector3.zero),
                angles = mat.rotation.eulerAngles,
                scale = mat.lossyScale
            };

            return result;
        }

        static void RegisterCustomConverter<TFrom,TTo>( System.Func<TFrom,TTo> func)
        {
            Dictionary<System.Type, System.Func<object,object>> converters = null;
            if( ! s_Converters.TryGetValue(typeof(TFrom),out converters))
            {
                converters = new Dictionary<System.Type, System.Func<object,object>>();
                s_Converters.Add(typeof(TFrom),converters);
            }

            converters.Add(typeof(TTo), t=> func((TFrom)t));
        }

        static object ConvertUnityObject(object value, Type toType)
        {
            var castedValue = (UnityObject)value;
            if( castedValue == null) // null object don't have necessarly the correct type
                return null;

            if( ! toType.IsInstanceOfType(value))
            {
                Debug.LogErrorFormat("Cannot cast from {0} to {1}", value.GetType(), toType);
                return null;
            }

            return value;
        }


        static object TryConvertPrimitiveType(object value,Type toType)
        {
            try
            {
                return Convert.ChangeType(value, toType);
            }
            catch (InvalidCastException)
            {
            }
            catch(OverflowException)
            {
            }

            return System.Activator.CreateInstance(toType);
        }

        static System.Func<object,object> GetConverter(Type fromType, Type toType)
        {
            if( typeof(UnityObject).IsAssignableFrom(fromType))
            {
                return t=>ConvertUnityObject(t,toType);
            }

            Dictionary<System.Type, System.Func<object,object>> converters = null;
            if( ! s_Converters.TryGetValue(fromType,out converters))
            {
                converters = new Dictionary<System.Type, System.Func<object,object>>();
                s_Converters.Add(fromType,converters);
            }


            System.Func<object,object> converter = null;
            if( ! converters.TryGetValue(toType,out converter))
            {
                if (fromType == toType || toType.IsAssignableFrom(fromType))
                {
                    converter = t => t;
                }
                else
                {
                    var implicitMethod = fromType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "op_Implicit" && m.ReturnType == toType);
                    if (implicitMethod != null)
                    {
                        converter = t=> implicitMethod.Invoke(null, new object[] { t });
                    }
                    else
                    {
                        implicitMethod = toType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                            .FirstOrDefault(m => m.Name == "op_Implicit" && m.GetParameters()[0].ParameterType == fromType && m.ReturnType == toType);
                        if (implicitMethod != null)
                        {
                            converter = t=> implicitMethod.Invoke(null, new object[] { t });
                        }
                    }
                    if( converter == null)
                    {
                        if (toType.IsPrimitive)
                        {
                            if( fromType.IsPrimitive )
                                converter = t => TryConvertPrimitiveType(t, toType);
                            else if( toType != typeof(float))
                            {
                                var floatConverter = GetConverter(fromType,typeof(float));
                                if( floatConverter != null)
                                {
                                    converter = t=> TryConvertPrimitiveType(floatConverter(t), toType);
                                }
                            }
                        }
                    }
                }
                converters.Add(toType,converter);
            }

            return converter;
        }

        public static object ConvertTo(object value, Type type)
        {
            if( value == null)
                return null;
            var fromType = value.GetType();

            var converter = GetConverter(fromType,type);

            if( converter == null )
            {
                Debug.LogErrorFormat("Cannot cast from {0} to {1}", fromType, type);
                return null;
            }

            return converter(value);
        }


        public static bool TryConvertTo(object value,Type type,out object result)
        {
            if( value == null)
            {
                result = null;
                return true;
            }
            var fromType = value.GetType();

            var converter = GetConverter(fromType,type);

            if( converter == null)
            {
                result = null;
                return false;
            }

            result = converter(value);

            return true;
        }
    }
}
