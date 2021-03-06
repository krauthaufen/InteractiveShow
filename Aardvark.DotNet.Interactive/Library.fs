﻿namespace Aardvark.DotNet.Interactive

open System
open FSharp.Data.Adaptive
open Microsoft.DotNet.Interactive
open Aardvark.Base
open Aardvark.UI
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering


[<AutoOpen>]
module Show =

    open FSharp.Data.Adaptive
    open System.Net
    open System.Text
    open Aardvark.UI
    open Aardvark.UI.Primitives
    open Aardvark.Application
    open Aardvark.Application.Slim
    open Suave
    open Suave.SuaveConfig
    open Suave.WebPart

    let inline noOut (action : unit -> 'a) : 'a =
        let o = System.Console.Out
        try
            System.Console.SetOut System.IO.TextWriter.Null
            action()
        finally
            System.Console.SetOut o

    let inline private toHTMLAux (a : ^a) (b : ^b) =
        noOut <| fun () -> ((^a or ^b) : (static member ToHTML : ^b -> string) (b))
        
    let inline private showAux (a : ^a) (b : ^b) =
        noOut <| fun () -> ((^a or ^b) : (static member Show : ^b -> ^c) (b))

    module Server =
        
        let private freePort () =
            let l = new System.Net.Sockets.TcpListener(IPAddress.Any, 0)
            l.Start()
            let ep = l.Server.LocalEndPoint :?> IPEndPoint
            l.Stop()
            ep.Port

        let private served = 
            let dict = System.Collections.Concurrent.ConcurrentDictionary<string, WebPart>()
            dict.["main"] <- fun r ->
                let text = System.Text.StringBuilder()
                for KeyValue(k, _v) in dict do
                    text.AppendLine($"{k} <br/>") |> ignore
                Successful.OK (string text) r

            dict
            
        let port = freePort()

        do
            let stop, start = 
                noOut <| fun () -> 
                    startWebServerAsync { defaultConfig with bindings = [ { scheme = Protocol.HTTP; socketBinding = { ip = IPAddress.Any; port = uint16 port } } ] } (fun r ->
                        let parts = r.request.path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        if parts.Length > 0 then 
                            let entry = parts.[0]
                            let newPath = parts |> Seq.skip 1 |> String.concat "/" |> sprintf "/%s"
                            let r1 = { r with request = { r.request with rawPath = newPath } }
                            match served.TryGetValue entry with
                            | (true, e) -> e r1
                            | _ -> Successful.OK entry r1
                        else
                            Successful.OK "HELLO" r
                    )

            async {
                let o = System.Console.Out
                try
                    System.Console.SetOut System.IO.TextWriter.Null
                    do! start
                finally
                    System.Console.SetOut o
            } |> Async.StartImmediate

        let serve (attributes : list<string * AttributeValue<_>>) (part : WebPart) =
            let name = System.Guid.NewGuid() |> string

            let atts =
                attributes |> List.choose (fun (key, value)->
                    match value with
                    | AttributeValue.String str -> 
                        let str = if key = "style" then "background-color: #37373D;" + str else str
                        Some (sprintf "%s=\"%s\"" key str)
                    | _ -> None
                )
                |> String.concat " "

            served.[name] <- part
            $"<iframe src=\"http://localhost:{port}/{name}/\" {atts}></iframe>"



    type Show private() =

        static do 
            noOut <| fun () ->
                IntrospectionProperties.CustomEntryAssembly <- typeof<ISg<_>>.Assembly
                Aardvark.Init()

        static let app = lazy (noOut <| fun () -> new OpenGlApplication())

        static member ToHTML (dom : DomNode<_>) =
            let runtime = app.Value.Runtime
            let app =   
                {
                    initial = ()
                    update = fun () _ -> ()
                    view = fun m -> dom
                    threads = fun _ -> ThreadPool.empty
                    unpersist =
                        {
                            create = fun () -> ()
                            update = fun () () -> ()
                        }
                }
            let mapp = App.start app

            noOut <| fun () -> 
                Server.serve [] (
                    MutableApp.toWebPart' runtime false mapp
                )

        static member ToHTML (img : PixImage) =
            use ms = new System.IO.MemoryStream()
            img.SaveAsImage(ms, PixFileFormat.Png)
            let data = ms.ToArray() |> System.Convert.ToBase64String
            $"<div><code>PixImage</code>&lt;<code>{img.PixFormat.Type.Name}</code>&gt;({img.Size.X}, {img.Size.Y}, <code>Col.Format.{img.Format}</code>)" +
            $"<div style='padding-left: 20px'><img onclick=\"event.target.src='';\" style='max-width: 100%%; max-height: 100%%' src='data:image/png;base64,{data}'></img></div></div>"

        static member MatrixToHTML<'a, 'b when 'b :> IMatrix<'a>> (mat : 'b, inner : 'a -> string) =
            let b = StringBuilder()

            b.Append $"<div><code>{mat.GetType().Name}</code>" |> ignore
            b.Append "<table style='width: 0px'>" |> ignore
            for r in 0L .. mat.Dim.Y - 1L do   
                b.Append "<tr>" |> ignore
                for c in 0L .. mat.Dim.X - 1L do
                    b.Append $"<td>{inner mat.[c,r]}</td>" |> ignore
                b.Append "</tr>" |> ignore

            b.Append "</table></div>" |> ignore
            b.ToString()


        static member VectorToHTML<'a, 'b when 'b :> IVector<'a>> (mat : 'b, inner : 'a -> string) =
            let b = StringBuilder()

            b.Append $"<div><code>{mat.GetType().Name}</code>" |> ignore
            b.Append "<table style='width: 0px'><tr>" |> ignore
            for r in 0L .. mat.Dim - 1L do   
                b.Append $"<td>{inner mat.[r]}</td>" |> ignore
            b.Append "</tr></table></div>" |> ignore
            b.ToString()

        static member OptionToHTML(value : option<'a>, inner : 'a -> string) =
            match value with
            | Some value -> 
                $"<div><code>Some</code><br /><div style='padding-left: 20px'>{inner value}</div></div"
            | None -> 
                "<code>None</code>"

        static member ToHTML (value : float) =
            $"{value}"

        static member ToHTML (value : string) =
            if isNull value then "null"
            else $"\"{value}\""

        static member ToHTML (value : float32) =
            $"{value}"

        static member ToHTML (vec : V2d) =
            $"<code>V2d({vec.X}, {vec.Y})</code>"

        static member inline ToHTML (mat : option<'a>) =
            Show.OptionToHTML(mat, toHTMLAux Unchecked.defaultof<Show>)

        static member inline ToHTML (mat : IMatrix<'a>) =
            Show.MatrixToHTML(mat, toHTMLAux Unchecked.defaultof<Show>)

        static member inline ToHTML (mat : IVector<'a>) =
            Show.VectorToHTML(mat, toHTMLAux Unchecked.defaultof<Show>)

        static member ToHTML(sg : ISg<_>, ?width: int, ?height: int, ?samples: int) =
            let samples = defaultArg samples 8
            let width = defaultArg width 720
            let height = defaultArg height 405

            let runtime = app.Value.Runtime
            let app =   
                {
                    initial = FreeFlyController.initial
                    update = FreeFlyController.update
                    view = fun m ->
                        let atts =
                            AttributeMap.ofList [
                                style "background-color: #37373D"
                                attribute "data-quality" "100"
                                attribute "data-samples" (string samples)
                            ]
                        FreeFlyController.controlledControl m id (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) atts sg
                    threads = FreeFlyController.threads
                    unpersist = Unpersist.instance
                }

            let mapp = App.start app

            noOut <| fun () -> 
                Server.serve [style $"width: {width}px; height: {height}px"] (
                    MutableApp.toWebPart' runtime false mapp
                )


    [<AutoOpen; AbstractClass; Sealed>]
    type ShowHelpers =
        static member show(sg : ISg<_>, ?width: int, ?height: int, ?samples: int) =
            let html = Show.ToHTML(sg, ?width = width, ?height = height, ?samples = samples)
            Kernel.display (
                Kernel.HTML html
            )

        static member inline show a =
            let html = toHTMLAux Unchecked.defaultof<Show> a
            Kernel.display (
                Kernel.HTML html
            )

            

    let inline toHTML a = toHTMLAux Unchecked.defaultof<Show> a

    let markdown (a : string) = Kernel.display(a, "text/markdown")