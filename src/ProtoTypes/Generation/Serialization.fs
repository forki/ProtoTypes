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

    let primitiveWriter = function
        | "double" -> <@@ Codec.writeDouble @@>
        | "float" -> <@@ Codec.writeFloat @@>
        | "int32" -> <@@ Codec.writeInt32 @@>
        | "int64" -> <@@ Codec.writeInt64 @@>
        | "uint32" -> <@@ Codec.writeUInt32 @@>
        | "uint64" -> <@@ Codec.writeUInt64 @@>
        | "sint32" -> <@@ Codec.writeSInt32 @@>
        | "sint64" -> <@@ Codec.writeSInt64 @@>
        | "fixed32" -> <@@ Codec.writeFixed32 @@>
        | "fixed64" -> <@@ Codec.writeFixed64 @@>
        | "sfixed32" -> <@@ Codec.writeSFixed32 @@>
        | "sfixed64" -> <@@ Codec.writeSFixed64 @@>
        | "bool" -> <@@ Codec.writeBool @@>
        | "string" -> <@@ Codec.writeString @@>
        | "bytes" -> <@@ Codec.writeBytes @@>
        | x -> notsupportedf "Primitive type '%s' is not supported" x
        
    /// Creates an expression that serializes all given properties to the given instance of ZeroCopyBuffer
    let private serializeProperty (prop: PropertyDescriptor) buffer this =
    
        let value = Expr.PropertyGet(this, prop.ProvidedProperty)
        let position = prop.Position
        
        // writer is an expression that represents a function 'T -> unit for any primitive or enum field of type 'T.
        // For embedded messages, writer will have type Message -> unit. It's caused by the fact that it's not possible to pass
        // any generic arguments including option<'T> and 'T -> unit to other functions if 'T is generated by a type provider.
        let writer =
            match prop.TypeKind with
                | Primitive -> primitiveWriter prop.ProtobufType
                | Class -> <@@ Codec.writeEmbedded @@>
                | Enum -> <@@ Codec.writeInt32 @@>
                
        let write f value = Expr.callStaticGeneric [prop.UnderlyingType] [writer; value] f
        
        let callPrimitive writer rule =
            let args =  [Expr.Value(position); buffer; value]
            match rule with
            | Required -> Expr.apply writer args
            | Optional -> 
                Expr.callStaticGeneric 
                    [prop.UnderlyingType]
                    (writer::args)
                    <@@ Codec.writeOptional x x x x @@> 
            | Repeated -> 
                Expr.callStaticGeneric 
                    [prop.UnderlyingType]
                    (writer::args)
                    <@@ Codec.writeRepeated x x x x @@> 
        try
        
            match prop.TypeKind, prop.Rule with
            | Class, Optional -> 
                Expr.callStaticGeneric 
                    [prop.UnderlyingType] 
                    [Expr.Value(position); buffer; Expr.Coerce(value, typeof<obj>)]  
                    <@@ Codec.writeOptionalEmbedded x x x @@>
            | Class, Repeated ->
                Expr.callStaticGeneric 
                    [prop.UnderlyingType] 
                    [Expr.Value(position); buffer; Expr.Coerce(value, typeof<obj>)]  
                    <@@ Codec.writeRepeatedEmbedded x x x @@>
            | Class, Required -> 
                <@@ Codec.writeEmbedded x x x @@> 
                |> Expr.getMethodDef 
                |> Expr.callStatic [Expr.Value(position); buffer; value]
            | Enum, rule -> callPrimitive <@@ Codec.writeInt32 @@> rule
            | Primitive, rule -> callPrimitive (primitiveWriter prop.ProtobufType) rule

            // match prop.Rule with
            // | Required -> Expr.Application(writer, value)
            // | Optional ->
            //     match prop.TypeKind with
            //     | Class -> write <@@ Codec.writeOptionalEmbedded x x @@> <| Expr.Coerce(value, typeof<obj>)
            //     | _ -> write <@@ Codec.writeOptional x x @@> value
            // | Repeated ->
            //     match prop.TypeKind with
            //     | Class -> write <@@ Codec.writeRepeatedEmbedded x x @@> <| Expr.Coerce(value, typeof<obj>)
            //     | _ -> write <@@ Codec.writeRepeated x x @@> value
        with
        | ex -> 
            printfn "Failed to serialize property %s: %O. Error: %O" prop.ProvidedProperty.Name value.Type ex
            reraise()

    let serializeExpr (typeInfo: TypeDescriptor) buffer this = 
        typeInfo.AllProperties
        |> List.sortBy (fun prop -> prop.Position)
        |> List.map (fun prop -> serializeProperty prop buffer this)
        |> Expr.sequence