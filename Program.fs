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
    |> Seq.filter (fun n -> n.InnerText = "DOWNLOAD")
    |> Seq.map
        (fun n ->
            Uri(
                "https://tenhou.net"
                + n.GetAttributeValue("href", "")
            ))

let httpClient = new HttpClient()

let getResponse tenhouId =
    httpClient.GetStringAsync($"https://tenhou.net//0/log/find.cgi?un={tenhouId}")
    |> Async.AwaitTask

let downloadReplay (url: Uri) (path: string) =
    async {
        try
            use webClient = new WebClient()
            let queryString = HttpUtility.ParseQueryString(url.Query)

            let fileName =
                queryString.Get("log")
                + "&tw="
                + queryString.Get("tw")
                + ".mjlog"

            let subdir = fileName.Substring(0, 6)
            let downloadPath = $"{path}/{subdir}"
            let fullPath = $"{downloadPath}/{fileName}"

            if File.Exists(fullPath) then
                return Ok None
            else
                Directory.CreateDirectory(downloadPath) |> ignore
                webClient.DownloadFile(url, fullPath)
                lock stdout (fun () -> printfn $"{url} ==>\n {fullPath}")
                return Ok(Some fullPath)
        with e -> return Error e.Message
    }

let downloadReplays urls path =
    urls
    |> Seq.map (fun url -> downloadReplay url path)
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Seq.collect
        (fun v ->
            match v with
            | Ok o -> Option.toList o
            | Error e ->
                printfn $"*** Error: {e}"
                [])

[<EntryPoint>]
let main argv =
    printfn "tenhou-dl v1.1.0"

    match argv with
    | [| tenhouId; path |] ->
        let urls =
            getResponse tenhouId
            |> Async.RunSynchronously
            |> parseResponse

        let count = downloadReplays urls path |> Seq.length
        printfn "\nDownloaded %d replay%s" count (if count <> 1 then "s" else "")
    | _ ->
        printfn
            "Usage: tenhou-dl <Tenhou ID> <Log path>
Example: tenhou-dl ID12345678-6fnB8AoP \"C:\\tenhou\\logs\\\""

    0
