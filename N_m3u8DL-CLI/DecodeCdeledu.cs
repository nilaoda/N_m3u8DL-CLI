using NiL.JS.BaseLibrary;
using NiL.JS.Core;
using NiL.JS.Extensions;
using System;
using Array = System.Array;

namespace N_m3u8DL_CLI
{
    internal class DecodeCdeledu
    {
        private static string JS = @"
var _keyStr = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';

var removePaddingChars = function(input) {
    var lkey = _keyStr.indexOf(input.charAt(input.length - 1));
    if (lkey == 64) {
        return input.substring(0, input.length - 1);
    }
    return input;
}

var base64Decode = function(input, arrayBuffer) {
    input = removePaddingChars(input);
    input = removePaddingChars(input);
    var bytes = parseInt((input.length / 4) * 3, 10);
    var uarray;
    var chr1, chr2, chr3;
    var enc1, enc2, enc3, enc4;
    var i = 0;
    var j = 0;
    if (arrayBuffer) {
        uarray = new Uint8Array(arrayBuffer);
    } else {
        uarray = new Uint8Array(bytes);
    }
    input = input.replace(/[^A-Za-z0-9\+\/\=]/g, '');
    for (i = 0; i < bytes; i += 3) {
        enc1 = _keyStr.indexOf(input.charAt(j++));
        enc2 = _keyStr.indexOf(input.charAt(j++));
        enc3 = _keyStr.indexOf(input.charAt(j++));
        enc4 = _keyStr.indexOf(input.charAt(j++));
        chr1 = (enc1 << 2) | (enc2 >> 4);
        chr2 = ((enc2 & 15) << 4) | (enc3 >> 2);
        chr3 = ((enc3 & 3) << 6) | enc4;
        uarray[i] = chr1;
        if (enc3 != 64)
            uarray[i + 1] = chr2;
        if (enc4 != 64)
            uarray[i + 2] = chr3;
    }
    return uarray;
}

var uint8ArrayToString = function(uDataArr) {
    var arrStr = '';
    for (var i = 0; i < uDataArr.length; i++) {
        arrStr += String.fromCharCode(uDataArr[i]);
    }
    return arrStr;
}

var decodeKey = function(dataKeyString) {
    var decodeArr = base64Decode(dataKeyString);
    var decodeArrString = uint8ArrayToString(decodeArr);
    return decodeArrString;
    if (decodeArrString.indexOf('|&|') > 0) {
        return decodeArrString;
    }
    return '';
}
";
        //https://video.cdeledu.com/js/lib/cdel.hls.min-1.0.js?v=1.3
        public static string DecodeKey(string txt)
        {
            var context = new Context();
            context.Eval(JS);
            var concatFunction = context.GetVariable("decodeKey").As<Function>();
            string key = concatFunction.Call(new Arguments { txt }).ToString();
            string realKey = key.Split(new string[] { "|&|" }, StringSplitOptions.None)[1];
            return realKey;
        }
    }
}
