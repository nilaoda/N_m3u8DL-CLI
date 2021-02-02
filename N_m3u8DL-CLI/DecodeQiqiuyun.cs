using NiL.JS.BaseLibrary;
using NiL.JS.Core;
using NiL.JS.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    //https://service-cdn.qiqiuyun.net/js-sdk-v2/media-core/hls/1.0.2/index.js
    class DecodeQiqiuyun
    {
        private static string JS1 = @"
function str2ab(str) {
   var s = encode_utf8(str)
   var buf = new ArrayBuffer(s.length); 
   var bufView = new Uint8Array(buf);
   for (var i=0, strLen=s.length; i<strLen; i++) {
     bufView[i] = s.charCodeAt(i);
   }
   return bufView;
 }
 
function encode_utf8(s) {
  return unescape(encodeURIComponent(s));
}

var J = function(e, t) {
            var r = [];
            return t.split(""-"").map((function(t) {
                r.push(e[parseInt(t)])
            }
            )),
            r
}
function decode(e) {
                    var t;e=str2ab(e);var Q=97;
                    if (20 === e.byteLength) {
                        var r = (t = e)[0]
                          , i = String.fromCharCode(r).toLowerCase()
                          , a = parseInt(i, 36) % 2
                          , n = t[a]
                          , s = String.fromCharCode(n)
                          , o = t[a + 1]
                          , l = String.fromCharCode(o)
                          , u = parseInt("""" + s + l, 36) % 3;
                        if (2 === u) {
                            var d = t[3]
                              , c = t[4]
                              , h = t[8]
                              , f = t[9]
                              , g = t[14]
                              , p = t[15]
                              , v = t[18]
                              , m = t[19]
                              , y = d - Q + 26 * (parseInt(String.fromCharCode(c), 10) + 1) - Q
                              , b = h - Q + 26 * (parseInt(String.fromCharCode(f), 10) + 1) - Q
                              , T = g - Q + 26 * (parseInt(String.fromCharCode(p), 10) + 1) - Q
                              , E = v - Q + 26 * (parseInt(String.fromCharCode(m), 10) + 2) - Q;
                            t = new Uint8Array([t[0], t[1], t[2], y, t[5], t[6], t[7], b, t[10], t[11], t[12], t[13], T, t[16], t[17], E])
                        } else if (1 === u) {
                            var S = new Uint8Array(J(t, ""0-1-2-3-4-12-13-14-7-6-18-17-15-8-9-10""));
                            t = S
                        } else {
                            if (0 !== u)
                                return;
                            var _ = new Uint8Array(J(t, ""0-1-2-12-13-14-15-16-17-18-4-5-6-7-9-10""));
                            t = _
                        }
                    } else if (17 === e.byteLength) {
                        t = t.slice(1);
                        var A = new Uint8Array(J(t, ""8-9-2-3-4-5-6-7-0-1-10-11-12-13-14-15""));
                        t = A
                    } else
                        t = e;
                    return t.join(',')}";

        //"2" == this.hls.config.version
        public static string DecodeKeyV1(string input)
        {
            var context = new Context();
            context.Eval(JS1);
            var concatFunction = context.GetVariable("decode").As<Function>();
            string keyStr = concatFunction.Call(new Arguments { input }).ToString();
            var key = new List<byte>();
            foreach (var ch in keyStr.Split(',')) key.Add((byte)Convert.ToInt32(ch));
            return Convert.ToBase64String(key.ToArray());
        }
    }
}
