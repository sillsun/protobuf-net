﻿#if !NO_RUNTIME
#if PORTABLE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProtoBuf.Serializers
{
    /// <summary>
    /// Manipulates with uris via reflection rather than strongly typed objects.
    /// This is because in PCLs, the Uri type may not match (WinRT uses Internal/Uri, .Net uses System/Uri)
    /// </summary>
    sealed class ReflectedUriDecorator : ProtoDecoratorBase
    {
        private readonly Type expectedType;

        private readonly PropertyInfo absoluteUriProperty;

        private readonly ConstructorInfo typeConstructor;

        public ReflectedUriDecorator(Type type, IProtoSerializer tail) : base(tail)
        {
            expectedType = type;

#if PROFILE259
			absoluteUriProperty = expectedType.GetRuntimeProperty("AbsoluteUri");
	        IEnumerable<ConstructorInfo> constructors = expectedType.GetTypeInfo().DeclaredConstructors;
	        typeConstructor = null;
			foreach(ConstructorInfo constructor in constructors)
			{
				ParameterInfo[] parameters = constructor.GetParameters();
				ParameterInfo parameterFirst = parameters.FirstOrDefault();
				Type stringType = typeof(string);
				if (parameterFirst != null && 
					parameterFirst.ParameterType == stringType)
				{
					typeConstructor = constructor;
					break;
				}
	        }
#else
			absoluteUriProperty = expectedType.GetProperty("AbsoluteUri");
            typeConstructor = expectedType.GetConstructor(new Type[] { typeof(string) });
#endif
		}
		public override Type ExpectedType { get { return expectedType; } }
        public override bool RequiresOldValue { get { return false; } }
        public override bool ReturnsValue { get { return true; } }
        
        public override void Write(object value, ProtoWriter dest)
        {
            Tail.Write(absoluteUriProperty.GetValue(value, null), dest);
        }
        public override object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // not expecting incoming
            string s = (string)Tail.Read(null, source);

            return s.Length == 0 ? null : typeConstructor.Invoke(new object[] { s });
        }

#if FEAT_COMPILER
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.LoadValue(valueFrom);
            ctx.LoadValue(absoluteUriProperty);
            Tail.EmitWrite(ctx, null);
        }
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Tail.EmitRead(ctx, valueFrom);
            ctx.CopyValue();
            Compiler.CodeLabel @nonEmpty = ctx.DefineLabel(), @end = ctx.DefineLabel();
            ctx.LoadValue(typeof(string).GetProperty("Length"));
            ctx.BranchIfTrue(@nonEmpty, true);
            ctx.DiscardValue();
            ctx.LoadNullRef();
            ctx.Branch(@end, true);
            ctx.MarkLabel(@nonEmpty);
            ctx.EmitCtor(expectedType, ctx.MapType(typeof(string)));
            ctx.MarkLabel(@end);
            
        }
#endif
    }
}
#endif
#endif