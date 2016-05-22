namespace ProtoTypes.Generation

open ProviderImplementation.ProvidedTypes

open Froto.Parser.Model
open Froto.Core.Encoding

type TypeKind = 
    | Primitive
    | Class
    | Enum

type ProtoPropertyInfo = 
    { ProvidedProperty: ProvidedProperty;
      Position: FieldNum;
      ProtoBufType: string;
      Rule: ProtoFieldRule; 
      TypeKind: TypeKind }
      
    member this.UnderlyingType =
        if this.ProvidedProperty.PropertyType.IsGenericType
        then this.ProvidedProperty.PropertyType.GenericTypeArguments.[0]
        else this.ProvidedProperty.PropertyType