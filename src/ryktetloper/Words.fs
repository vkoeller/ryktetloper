module ryktetloper.Words

open System
open System.IO

let words = File.ReadAllLines("words")

let rec randomWord (notIn: string[]) =
    let word = words[Random.Shared.NextInt64(0, words.Length) |> int]
    if Array.contains word notIn then
        randomWord notIn
    else
        word
