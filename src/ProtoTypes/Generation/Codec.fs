namespace ProtoTypes.Generation

open System

open Froto.Core
open Froto.Core.Encoding

open ProtoTypes.Core

// scalar type aliases based on https://developers.google.com/protocol-buffers/docs/proto3#scalar
type proto_double = float
type proto_float = float32
type proto_int32 = int
type proto_int64 = int64
type proto_uint32 = uint32
type proto_uint64 = uint64
type proto_sint32 = int
type proto_sint64 = int64
type proto_fixed32 = uint32
type proto_fixed64 = uint64
type proto_sfixed32 = int
type proto_sfixed64 = uint64
type proto_bool = bool
type proto_string = string
type proto_bytes = ArraySegment<byte>

type Writer<'T> = int -> ZeroCopyBuffer -> 'T -> unit
type Reader<'T> = RawField -> 'T

/// Contains helper functions to read/write values to/from ZeroCopyBuffer
[<RequireQualifiedAccess>]
module Codec =

    let private write f (fieldNumber: int) (buffer: ZeroCopyBuffer) value =
        f fieldNumber value buffer |> ignore 

    let writeDouble: Writer<proto_double> = write Serializer.dehydrateDouble
    let writeFloat: Writer<proto_float> = fun _ -> notsupportedf "float32 is currently not supported"
    let writeInt32: Writer<proto_int32> = write Serializer.dehydrateVarint
    let writeInt64: Writer<proto_int64> = write Serializer.dehydrateVarint
    let writeUInt32: Writer<proto_uint32> = write Serializer.dehydrateVarint
    let writeUInt64: Writer<proto_uint64> = write Serializer.dehydrateVarint
    let writeSInt32: Writer<proto_sint32> = write Serializer.dehydrateSInt32
    let writeSInt64: Writer<proto_sint64> = write Serializer.dehydrateSInt64
    // TODO Maybe should be fixed in Froto?
    let writeFixed32: Writer<proto_fixed32> = fun f -> fun b -> fun v -> Serializer.dehydrateFixed32 f (int v) b |> ignore
    let writeFixed64: Writer<proto_fixed64> = fun f -> fun b -> fun v -> Serializer.dehydrateFixed64 f (int v) b |> ignore
    let writeSFixed32: Writer<proto_sfixed32> = write Serializer.dehydrateFixed32
    let writeSFixed64: Writer<proto_sfixed64> = fun _ -> notsupportedf "sfixed64 is currently not supported"
    let writeBool: Writer<proto_bool> = write Serializer.dehydrateBool
    let writeString: Writer<proto_string> = write Serializer.dehydrateString
    let writeBytes: Writer<proto_bytes> = write Serializer.dehydrateBytes

    /// Serializes optional field using provided function to handle inner value if present
    let writeOptional writeInner value =
        match value with
        | Some(v) -> writeInner v
        | None -> ()
        
    /// Value is expected to be of type option<'T>. It's not possible
    /// to use this type directly in the signature because of type providers limitations.
    /// All optional non-generated types (i.e. primitive types and enums) should be serialized using
    /// more strongly-typed writeOptional function
    let writeOptionalEmbedded<'T when 'T :> Message> (writeInner: Message -> unit) (value: obj) =
        if value <> null 
        then value :?> option<'T> |> Option.get |> writeInner
        
    let writeRepeated writeItem values =
        for value in values do writeItem value
        
    let writeRepeatedEmbedded<'T when 'T :> Message> (writeInner: Message -> unit) (value: obj) =
        value :?> list<'T> |> List.iter writeInner

    let writeEmbedded fieldNumber buffer (message: Message) = 
        buffer
        |> WireFormat.encodeTag fieldNumber WireType.LengthDelimited
        |> WireFormat.encodeVarint (uint64 message.SerializedLength)
        |> message.Serialize
        |> ignore 

    let decodeFields (zcb: ZeroCopyBuffer) = seq {
        while (not zcb.IsEof) && zcb.Array.[int zcb.Position] > 7uy do
            yield WireFormat.decodeField zcb
    }
    
    let private readField<'T> f field = 
        let result = ref Unchecked.defaultof<'T>
        f result field
        !result

    let deserialize<'T when 'T :> Message and 'T : (new: unit -> 'T)> buffer =
        let x = new 'T()
        x.ReadFrom buffer |> ignore
        x

    let readDouble: Reader<proto_double> = readField Serializer.hydrateDouble
    let readFloat: Reader<proto_float> = fun _ -> notsupportedf "float32 is currently not supported"
    let readInt32: Reader<proto_int32> = readField Serializer.hydrateInt32
    let readInt64: Reader<proto_int64> = readField Serializer.hydrateInt64
    let readUInt32: Reader<proto_uint32> = readField Serializer.hydrateUInt32
    let readUInt64: Reader<proto_uint64> = readField Serializer.hydrateUInt64
    let readSInt32: Reader<proto_sint32> = readField Serializer.hydrateSInt32
    let readSInt64: Reader<proto_sint64> = readField Serializer.hydrateSInt64
    let readFixed32: Reader<proto_fixed32> = readField Serializer.hydrateFixed32
    let readFixed64: Reader<proto_fixed64> = readField Serializer.hydrateFixed64
    let readSFixed32: Reader<proto_sfixed32> = readField Serializer.hydrateSFixed32
    let readSFixed64: Reader<proto_sfixed64> = readField Serializer.hydrateSFixed64 >> uint64
    let readBool: Reader<proto_bool> = readField Serializer.hydrateBool
    let readString: Reader<proto_string> = readField Serializer.hydrateString
    let readBytes: Reader<proto_bytes> = readField Serializer.hydrateBytes >> proto_bytes
    
    let readEmbedded<'T when 'T :> Message and 'T : (new: unit -> 'T)> field = 
        match field with
        | LengthDelimited(_, segment) -> ZeroCopyBuffer segment |> deserialize<'T>
        | _ -> failwithf "Invalid format of the field: %O" field