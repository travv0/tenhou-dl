#!/usr/bin/env -S dotnet fsi

#r "nuget: HtmlAgilityPack, 1.11"

open HtmlAgilityPack
open System
open System.IO
open System.Net
open System.Net.Http
open System.Web

printfn "tenhou-dl v2.0.0"

let parseResponse response =
    let html = HtmlDocument()
    html.LoadHtml(response)
    let root = html.DocumentNode

    query {
        for n in root.Descendants("a") do
            where (n.InnerText = "DOWNLOAD")

            select (
                Uri(
                    "https://tenhou.net"
                    + n.GetAttributeValue("href", "")
                )
            )
    }

let httpClient = new HttpClient()
let webClient = new WebClient()

let getResponse tenhouId =
    httpClient.GetStringAsync(sprintf "https://tenhou.net/0/log/find.cgi?un=%s" tenhouId)
    |> Async.AwaitTask

let downloadReplay (url: Uri) path =
    try
        let queryString = HttpUtility.ParseQueryString(url.Query)

        let fileName =
            queryString.Get("log")
            + "&tw="
            + queryString.Get("tw")
            + ".mjlog"

        let subdir = fileName.Substring(0, 6)
        let downloadPath = Path.Join(path, subdir)
        let fullPath = Path.Join(downloadPath, fileName)

        if File.Exists(fullPath) then
            Ok None
        else
            Directory.CreateDirectory(downloadPath) |> ignore
            webClient.DownloadFile(url, fullPath)
            lock stdout (fun () -> printfn "%s ==>\n %s" url.AbsoluteUri fullPath)
            Ok(Some fullPath)
    with
    | e -> Error e.Message

let downloadReplays urls path =
    urls
    |> Seq.toArray
    |> Array.Parallel.collect (fun url ->
        match downloadReplay url path with
        | Ok o -> Option.toArray o
        | Error e ->
            lock stdout (fun () -> printfn "*** Error: %s" e)
            [||])

match fsi.CommandLineArgs |> Array.toList with
| [ _; tenhouId; path ] ->
    let urls =
        getResponse tenhouId
        |> Async.RunSynchronously
        |> parseResponse

    let count =
        downloadReplays urls path |> Array.length

    printfn "\nDownloaded %d replay%s" count (if count <> 1 then "s" else "")
| scriptName :: _ ->
    printfn
        $"Usage: %s{scriptName} <Tenhou ID> <Log path>
Example: %s{scriptName} ID12345678-6fnB8AoP \"C:\\tenhou\\logs\\\""
| [] -> assert false
