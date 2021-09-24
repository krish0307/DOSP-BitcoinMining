module Util

open System.Text
open System.Security.Cryptography
open System

let prefix="ktakkillapati"

let rec prefixZeroes (text:string,numOfZeroes:int) =
        let rec construct s n =
            match n with
            | _ when n <= 0 -> ""
            | _ -> s + (construct s (n-1))
        
        text.StartsWith(construct "0" numOfZeroes)

let randomStr = 
    let chars = "ABCDEFGHIJKLMNOPQRSTUVWUXYZ"
    let charsLen = chars.Length
    let random = Random()

    fun len -> 
        let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
        String(randomChars)

module String = 
   let ofChars arr = 
      arr |> Array.fold (fun acc elem -> sprintf "%s%c" acc elem) ""

let randomString len =

   let rand = System.Random()
   let chars = Array.append [|'A'..'Z'|] [|'0'..'9'|] 
               |> String.ofChars

   Array.init len (fun _ -> chars.[rand.Next(chars.Length)])
   |> String.ofChars
   
let computeSha256Hash (input:string) =
    let sha256 = SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(input))
    let hexFormat = Array.map (fun (x : byte) -> String.Format("{0:X2}", x)) sha256
    let hexSha256 = String.concat "" hexFormat
    hexSha256
