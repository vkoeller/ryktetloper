module ryktetloper.Json

open System.Text.Json
open System.Text.Json.Serialization

let jsonOptions =
    JsonFSharpOptions.Default()
        .WithUnionInternalTag()
        .WithUnionTagName("type")
        .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
        .WithUnionFieldNamingPolicy(JsonNamingPolicy.CamelCase)
        .WithUnionNamedFields()
        .WithUnionUnwrapSingleFieldCases()
        .WithUnionUnwrapSingleFieldCases()
        .WithUnionUnwrapRecordCases()
        .WithUnionUnwrapFieldlessTags()
        .ToJsonSerializerOptions()


let serialize d =
    JsonSerializer.Serialize(d, jsonOptions)

