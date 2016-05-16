﻿namespace ProtoTypes.Generation

open Microsoft.FSharp.Quotations

open ProtoTypes.Core
open ProviderImplementation.ProvidedTypes

open Froto.Parser.Model
open Froto.Core
open Froto.Core.Encoding

/// Contains an implementation of serialization method for types generated from ProtoBuf messages
[<RequireQualifiedAccess>]
module Serialization = 

    let primitiveWriter position buffer = function
        | "double" -> <@@ Codec.writeDouble position %%buffer @@>
        | "float" -> <@@ Codec.writeFloat position %%buffer @@>
        | "int32" -> <@@ Codec.writeInt32 position %%buffer @@>
        | "int64" -> <@@ Codec.writeInt64 position %%buffer @@>
        | "uint32" -> <@@ Codec.writeUInt32 position %%buffer @@>
        | "uint64" -> <@@ Codec.writeUInt64 position %%buffer @@>
        | "sint32" -> <@@ Codec.writeSInt32 position %%buffer @@>
        | "sint64" -> <@@ Codec.writeSInt64 position %%buffer @@>
        | "fixed32" -> <@@ Codec.writeFixed32 position %%buffer @@>
        | "fixed64" -> <@@ Codec.writeFixed64 position %%buffer @@>
        | "sfixed32" -> <@@ Codec.writeSFixed32 position %%buffer @@>
        | "sfixed64" -> <@@ Codec.writeSFixed64 position %%buffer @@>
        | "bool" -> <@@ Codec.writeBool position %%buffer @@>
        | "string" -> <@@ Codec.writeString position %%buffer @@>
        | "bytes" -> <@@ Codec.writeBytes position %%buffer @@>
        | x -> notsupportedf "Primitive type '%s' is not supported" x
        
    /// Creates an expression that serializes all given properties to the given instance of ZeroCopyBuffer
    let private serializeProperty (prop: ProtoPropertyInfo) buffer this =
    
        let value = Expr.PropertyGet(this, prop.ProvidedProperty)
        let position = prop.ProtoField.Position
        
        // writer is an expression that represents a function 'T -> unit for any primitive or enum field of type 'T.
        // For embedded messages, writer will have type Message -> unit. It's caused by the fact that it's not possible to pass
        // any generic arguments including option<'T> and 'T -> unit to other functions if 'T is generated by a type provider.
        let writer =
            match prop.TypeKind with
            | Primitive -> primitiveWriter position buffer prop.ProtoField.Type
            | Class -> <@@ Codec.writeEmbedded position %%buffer @@>
            | Enum -> <@@ Codec.writeInt32 position %%buffer @@>
                
        let write f value = Expr.callStaticGeneric [prop.UnderlyingType] [writer; value] f
        
        let value' = 
            match prop.TypeKind with
            | Class -> Expr.Coerce(value, typeof<obj>)
            | Enum -> Expr.Coerce(value, typeof<proto_int32>)
            | Primitive -> value

        try
            match prop.ProtoField.Rule with
            | Required -> Expr.Application(writer, value')
            | Optional ->
                match prop.TypeKind with
                | Class -> write <@@ Codec.writeOptionalEmbedded x x @@> value'
                | _ -> write <@@ Codec.writeOptional x x @@> value'
            | Repeated ->
                match prop.TypeKind with
                | Class -> write <@@ Codec.writeRepeatedEmbedded x x @@> <| value'
                | _ -> write <@@ Codec.writeRepeated x x @@> value'
        with
        | ex -> 
            printfn 
                "Failed to serialize property %s: %O (coerced to: %O). Error: %O" 
                prop.ProvidedProperty.Name 
                value.Type value'.Type 
                ex
            reraise()
            
    let serializeExpr properties buffer this =
        let serializeProperties = 
            properties
            |> List.sortBy (fun prop -> prop.ProtoField.Position)
            |> List.map (fun prop -> serializeProperty prop buffer this)
            |> Expr.sequence 

        Expr.Sequential(serializeProperties, buffer)