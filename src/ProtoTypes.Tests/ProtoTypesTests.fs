﻿[<NUnit.Framework.TestFixture>]
module ProtoTypes.Tests

open System

open NUnit.Framework
open FsUnit

open Froto.Core

type Proto = ProtoTypes.ProtocolBuffersTypeProvider<"proto/person.proto">
type Sample = Proto.ProtoTypes.Sample

[<Test>]
let ``Person test``() =
    let address = Sample.Person.Address("Street", 12, [1; 2; 3], [Sample.Person.IntContainer(5); Sample.Person.IntContainer(7)])
    let p = Sample.Person("Name", 1, false, 82.3, Sample.Person.Gender.Female, Some "Email", Some address)
    p.Name |> should be (equal "Name")
    p.PersonGender |> should be (equal Sample.Person.Gender.Female)
    p.PersonAddress.Value.Address1 |> should be (equal "Street")
    
[<Test>]
let ``Serialization test``() =
    let address = Sample.Person.Address("Street", 12, [1; 2; 3], [Sample.Person.IntContainer(5); Sample.Person.IntContainer(7)])
    let person = Sample.Person("Name", 1, false, 82.3, Sample.Person.Gender.Female, Some "Email", Some address)
    
    let buffer = ZeroCopyBuffer(1000)
    person.Serialize(buffer)
    
    buffer.Position |> should be (greaterThan 0)
    