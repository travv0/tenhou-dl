open FSharpPlus
open HtmlAgilityPack
open System
open System.IO
open System.Net
open System.Net.Http
open System.Web

let parseResponse response =
    let html = HtmlDocument()
    html.LoadHtml(response)
    let root = html.DocumentNode

    root.Descendants("a")
    |> filter (fun n -> n.InnerText = "DOWNLOAD")
    |> map
        (fun n ->
            Uri(
                "https://tenhou.net"
                + n.GetAttributeValue("href", "")
            ))

let httpClient = new HttpClient()

let getResponse tenhouId =
    httpClient.GetStringAsync(sprintf "https://tenhou.net/0/log/find.cgi?un=%s" tenhouId)
    |> Async.AwaitTask

let downloadReplay (url: Uri) path =
    try
        use webClient = new WebClient()
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
    with e -> Error e.Message

let downloadReplays urls path =
    urls
    |> toArray
    |> Array.Parallel.collect
        (fun url ->
            match downloadReplay url path with
            | Ok o -> toArray o
            | Error e ->
                lock stdout (fun () -> printfn "*** Error: %s" e)
                empty)

[<EntryPoint>]
let main argv =
    printfn "tenhou-dl v1.1.2"

    match argv with
    | [| tenhouId; path |] ->
        let urls =
            getResponse tenhouId
            |> Async.RunSynchronously
            |> parseResponse

        let count = downloadReplays urls path |> length
        printfn "\nDownloaded %d replay%s" count (if count <> 1 then "s" else "")
    | _ ->
        printfn
            "Usage: tenhou-dl <Tenhou ID> <Log path>
Example: tenhou-dl ID12345678-6fnB8AoP \"C:\\tenhou\\logs\\\""

    0
