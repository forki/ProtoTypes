﻿[<NUnit.Framework.TestFixture>]
module ProtoTypes.Tests

open System
open System.Collections.Generic

open NUnit.Framework
open FsUnit

open Froto.Core

open ProtoTypes
open ProtoTypes.Core
open ProtoTypes.Generation

type Proto = ProtocolBuffersTypeProvider<"proto/person.proto">
type Sample = Proto.ProtoTypes.Sample

let private createPerson() =
    let address = 
        Sample.Person.Address(
            Address1 = "Street", 
            HouseNumber = 12, 
            Whatever = [1; 2; 3], 
            SomeInts = [Sample.Person.IntContainer(Value = 5); Sample.Person.IntContainer(Value = 7)])

    Sample.Person(
        Name = "Name",
         Id = 1,
         HasCriminalConvictions = false,
         Weight = 82.3, 
         PersonGender = Sample.Person.Gender.Female, 
         Email = Some "Email", 
         PersonAddress = Some address)

let serializeDeserialize<'T when 'T :> Message> (msg: 'T) (deserialize: ZeroCopyBuffer -> 'T) =
    let buffer = ZeroCopyBuffer 1000
    msg.Serialize buffer
    
    let buffer' = ZeroCopyBuffer buffer.AsArraySegment
    deserialize buffer'

[<Test>]
let ``Person test``() =
    let person = createPerson()
    person.Name |> should be (equal "Name")
    person.PersonGender |> should be (equal Sample.Person.Gender.Female)
    person.PersonAddress.Value.Address1 |> should be (equal "Street")
    
    
[<Test>]
let ``Serialization test``() =
    let person = createPerson()
    
    let buffer = ZeroCopyBuffer 1000
    person.Serialize buffer |> ignore
    buffer.Position |> should be (greaterThan 0)

[<Test>]
let ``Deserialization test``() =
    let person = createPerson()
    let person' = serializeDeserialize person Sample.Person.Deserialize
    
    person'.Name |> should be (equal person.Name)
    person'.Id |> should be (equal person.Id)
    person'.HasCriminalConvictions |> should be (equal person.HasCriminalConvictions)
    person'.Weight |> should be (equal person.Weight)
    person'.PersonGender |> should be (equal person.PersonGender)
    person'.Email |> should be (equal person.Email)
    
    person'.PersonAddress.IsSome |> should be True
    let address = person.PersonAddress.Value
    let address' = person'.PersonAddress.Value
    address'.Address1 |> should be (equal address.Address1)
    address'.HouseNumber |> should be (equal address.HouseNumber)
    address'.Whatever |> should be (equal address.Whatever)
    address'.SomeInts |> List.map (fun v -> v.Value) |> should be (equal (address.SomeInts |> List.map(fun v -> v.Value)))
    
    
[<Test>]
let ``Deserialize None optional value``() =
    let person = createPerson()
    person.PersonAddress <- None
    let person' = serializeDeserialize person Sample.Person.Deserialize
    
    person'.PersonAddress.IsSome |> should be False
    

[<Test>]
let ``Deserialize empty repeated value``() =
    let person = createPerson()
    let address = person.PersonAddress.Value
    
    address.SomeInts <- []
    address.Whatever <- []

    let address' = serializeDeserialize address Sample.Person.Address.Deserialize
    
    address'.SomeInts |> should be Empty
    address'.Whatever |> should be Empty

[<Test>]
let ``Primitive types``() = 
    let container = 
        Sample.PrimitiveContainer(
            DoubleField = 1.2,
            Int32Field = 42,
            Int64Field = 12351L,
            Uint32Field = 123124ul,
            Uint64Field = 1146111UL,
            Sint32Field = 1112,
            Sint64Field = -1236134L,
            Fixed32Field = proto_fixed32.MaxValue,
            Fixed64Field = proto_fixed64.MaxValue,
            Sfixed32Field = proto_sfixed32.MinValue,
            Sfixed64Field = proto_sfixed64.MinValue,
            BoolField = true,
            StringField = "string field value",
            BytesField = ArraySegment [| 1uy; 2uy; 42uy |])
    
    let container' = serializeDeserialize container Sample.PrimitiveContainer.Deserialize
    
    container'.DoubleField |> should be (equal container.DoubleField)
    
    container'.Int32Field |> should be (equal container.Int32Field)
    container'.Int64Field |> should be (equal container.Int64Field)
    
    container'.Uint32Field |> should be (equal container.Uint32Field)
    container'.Uint64Field |> should be (equal container.Uint64Field)
    
    container'.Sint32Field |> should be (equal container.Sint32Field)
    container'.Sint64Field |> should be (equal container.Sint64Field)
    
    container'.Fixed32Field |> should be (equal container.Fixed32Field)
    container'.Fixed64Field |> should be (equal container.Fixed64Field)
    
    container'.Sfixed32Field |> should be (equal container.Sfixed32Field)
    container'.Sfixed64Field |> should be (equal container.Sfixed64Field)
    
    container'.BoolField |> should be (equal container.BoolField)
    container'.StringField |> should be (equal container.StringField)
    container'.BytesField |> should be (equal container.BytesField)
    
type ValueOneofCase = Sample.OneOfContainer.ValueOneofCase

[<Test>]
let ``Oneof properties test``() =
    let oneofContainer = Sample.OneOfContainer()
    oneofContainer.ValueCase |> should be (equal ValueOneofCase.None)
    
    oneofContainer.Text <- Some "text"
    oneofContainer.Text |> should be (equal <| Some "text")
    oneofContainer.ValueCase |> should be (equal ValueOneofCase.Text)
    oneofContainer.Identifier.IsSome |> should be False
    
    oneofContainer.Identifier <- Some 10
    oneofContainer.Identifier |> should be (equal <| Some 10)
    oneofContainer.Text.IsSome |> should be False
    oneofContainer.ValueCase |> should be (equal ValueOneofCase.Identifier)
    
    oneofContainer.Identifier <- None
    oneofContainer.Identifier.IsSome |> should be False
    oneofContainer.Text.IsSome |> should be False
    oneofContainer.ValueCase |> should be (equal ValueOneofCase.None)
    
    oneofContainer.Identifier <- Some 10
    oneofContainer.ClearValue()
    oneofContainer.ValueCase |> should be (equal ValueOneofCase.None)
    oneofContainer.Identifier.IsSome |> should be False
    oneofContainer.Text.IsSome |> should be False
    
[<Test>]
let ``Oneof properties serialization test``() = 
    let oneofContainer = Sample.OneOfContainer()
    oneofContainer.Identifier <- Some 42
    oneofContainer.AnotherText <- "Some another text"
    
    let buffer = ZeroCopyBuffer 1000
    oneofContainer.Serialize buffer
    let oneofContainer' = Sample.OneOfContainer.Deserialize <| ZeroCopyBuffer buffer.AsArraySegment
    
    oneofContainer.Identifier |> should be (equal oneofContainer'.Identifier)
    oneofContainer.AnotherText |> should be (equal oneofContainer'.AnotherText)
    oneofContainer.ValueCase |> should be (equal oneofContainer'.ValueCase)
    
[<Test>]
let ``Map test``() =
    let mapContainer = Sample.MapContainer()
    let map = proto_concrete_map<_, _>()
    map.Add(1, "foo")
    map.Add(2, "bar")
    mapContainer.PrimitiveMap <- map
    
    let people = proto_concrete_map<_, _>()
    people.Add("Vasya", createPerson())
    mapContainer.People <- people
    
    let switches = proto_concrete_map<_, _>()
    switches.Add(1, Sample.MapContainer.Switch.On)
    mapContainer.Switches <- switches
    
    let buffer = mapContainer.SerializedLength |> int |> ZeroCopyBuffer
    mapContainer.Serialize buffer
    
    let mapContainer' = Sample.MapContainer.Deserialize <| ZeroCopyBuffer buffer.AsArraySegment
    
    mapContainer'.PrimitiveMap |> should be (not' Null)
    mapContainer'.PrimitiveMap.[1] |> should be (equal "foo")
    mapContainer'.PrimitiveMap.[2] |> should be (equal "bar")
    
    mapContainer'.Switches |> should be (not' Null)
    mapContainer'.Switches |> should haveCount 1
    mapContainer'.Switches.[1] |> should be (equal Sample.MapContainer.Switch.On)
    
    mapContainer'.People |> should be (not' Null)
    mapContainer'.People |> should haveCount 1
    mapContainer'.People.["Vasya"].Name |> should be (equal "Name")