// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Diagnostics
open System.Globalization
open System.Net
open System.Text.RegularExpressions
open FSharp.Data
open System.IO
// Define a function to construct a message to print

let formatDayOfWeek dow =
    match dow with
    | DayOfWeek.Monday -> "Montag"
    | DayOfWeek.Tuesday -> "Dienstag"
    | DayOfWeek.Wednesday -> "Mittwoch"
    | DayOfWeek.Thursday -> "Donnerstag"
    | DayOfWeek.Friday -> "Freitag"
    | DayOfWeek.Saturday -> "Samstag"
    | DayOfWeek.Sunday -> "Sonntag"
    | _ -> failwith "invalid day of week"

type PickUpDate =
    { date: DateTime
      garbageType: string }
    static member fromStringTuple(d, (g: string)) =
        let m = Regex.Match(d, "\d+\.\d+\.\d+")

        let date =
            DateTime.Parse(m.Value, CultureInfo("de-DE"))

        let garbageType =
            Regex.Match(g, @"([\w\s]+) \(").Groups.[1].Value

        { date = date
          garbageType = garbageType }

    member this.isInTheNextSevenDays : bool =
        this.date < DateTime.Now.Add(TimeSpan.FromDays(7.))

    member this.ToString : string =
        sprintf
            $"{this.garbageType}, \
            {formatDayOfWeek this.date.DayOfWeek} ({this.date.Day}.{this.date.Month}.{this.date.Year})"


let getWebPage =
    let r =
        Http.RequestString(
            "https://www.st-leonhard-forst.gv.at/Abfuhrtermine",
            httpMethod = "GET",
            headers = [ "Cookie", "ris_cookie_setting=g7750" ]
        )

    r

let extractRelevantPart webPage =
    let html = HtmlDocument.Parse webPage

    let rows = (html.CssSelect "td.td_kal")

    let texts =
        [ for row in rows do
              HtmlNode.innerText row ]

    let chunked = List.chunkBySize 3 texts

    [ for chunk in chunked do
          match chunk with
          | [ a; b; _ ] -> (a, b)
          | _ -> failwith "invalid chunk size" ]

let genMessageText (pickUpDates: list<PickUpDate>) =
    "Abfuhrtermine nächste Woche: \n"
    + if pickUpDates.Length <> 0 then
          (String.Join(
              "\n",
              [ for pd in pickUpDates do
                    "  - " + pd.ToString ]
          ))
      else
          "Keine Abfuhrtermine"

let SIGNAL_USER = "signal_user" // has to be present on the system
let TARGET_PHONE_NUMBER = "target_user" 

[<EntryPoint>]
let main argv =
    let webPage = getWebPage
    let relevantPart = extractRelevantPart webPage

    let pickUpDates =
        [ for tuple in relevantPart do
              PickUpDate.fromStringTuple tuple ]

    let pickUpDatesNextDays =
        [ for pud in pickUpDates do
              if pud.isInTheNextSevenDays then pud ]

    let text = genMessageText pickUpDatesNextDays
    printfn "%s" (text)

    let startInfo =
        ProcessStartInfo("signal-cli", $"-u {SIGNAL_USER} send -m \"{text}\" {TARGET_PHONE_NUMBER}")

    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    let p = new Process()
    p.StartInfo <- startInfo
    p.Start() |> ignore

    let r = p.StandardOutput
    let e = p.StandardError
    printfn "%s" (r.ReadToEnd())
    printfn "%s" (e.ReadToEnd())

    0 // return an integer exit code
