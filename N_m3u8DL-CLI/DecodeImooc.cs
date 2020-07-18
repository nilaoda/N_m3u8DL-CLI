using NiL.JS.BaseLibrary;
using NiL.JS.Core;
using NiL.JS.Extensions;
using System;
using Array = System.Array;

namespace N_m3u8DL_CLI
{
    /*
     * js代码来自：https://www.imooc.com/static/moco/player/3.0.6.3/mocoplayer.js?v=202006122046
     * 
     */
    class DecodeImooc
    {
        private static string JS = @"
function n(t, e) {
                function r(t, e) {
                    var r = '';
                    if ('object' == typeof t)
                        for (var n = 0; n < t.length; n++)
                            r += String.fromCharCode(t[n]);
                    t = r || t;
                    for (var i, o, a = new Uint8Array(t.length), s = e.length, n = 0; n < t.length; n++)
                        o = n % s,
                        i = t[n],
                        i = i.toString().charCodeAt(0),
                        a[n] = i ^ e.charCodeAt(o);
                    return a
                }
                function n(t) {
                    var e = '';
                    if ('object' == typeof t)
                        for (var r = 0; r < t.length; r++)
                            e += String.fromCharCode(t[r]);
                    t = e || t;
                    var n = new Uint8Array(t.length);
                    for (r = 0; r < t.length; r++)
                        n[r] = t[r].toString().charCodeAt(0);
                    var i, o, r = 0;
                    for (r = 0; r < n.length; r++)
                        0 != (i = n[r] % 3) && r + i < n.length && (o = n[r + 1],
                        n[r + 1] = n[r + i],
                        n[r + i] = o,
                        r = r + i + 1);
                    return n
                }
                function i(t) {
                    var e = '';
                    if ('object' == typeof t)
                        for (var r = 0; r < t.length; r++)
                            e += String.fromCharCode(t[r]);
                    t = e || t;
                    var n = new Uint8Array(t.length);
                    for (r = 0; r < t.length; r++)
                        n[r] = t[r].toString().charCodeAt(0);
                    var r = 0
                      , i = 0
                      , o = 0
                      , a = 0;
                    for (r = 0; r < n.length; r++)
                        o = n[r] % 2,
                        o && r++,
                        a++;
                    var s = new Uint8Array(a);
                    for (r = 0; r < n.length; r++)
                        o = n[r] % 2,
                        s[i++] = o ? n[r++] : n[r];
                    return s
                }
                function o(t, e) {
                    var r = 0
                      , n = 0
                      , i = 0
                      , o = 0
                      , a = '';
                    if ('object' == typeof t)
                        for (var r = 0; r < t.length; r++)
                            a += String.fromCharCode(t[r]);
                    t = a || t;
                    var s = new Uint8Array(t.length);
                    for (r = 0; r < t.length; r++)
                        s[r] = t[r].toString().charCodeAt(0);
                    for (r = 0; r < t.length; r++)
                        if (0 != (o = s[r] % 5) && 1 != o && r + o < s.length && (i = s[r + 1],
                        n = r + 2,
                        s[r + 1] = s[r + o],
                        s[o + r] = i,
                        (r = r + o + 1) - 2 > n))
                            for (; n < r - 2; n++)
                                s[n] = s[n] ^ e.charCodeAt(n % e.length);
                    for (r = 0; r < t.length; r++)
                        s[r] = s[r] ^ e.charCodeAt(r % e.length);
                    return s
                }
                for (var a = {
                    data: {
                        info: t
                    }
                }, s = {
                    q: r,
                    h: n,
                    m: i,
                    k: o
                }, l = a.data.info, u = l.substring(l.length - 4).split(''), c = 0; c < u.length; c++)
                    u[c] = u[c].toString().charCodeAt(0) % 4;
                u.reverse();
                for (var d = [], c = 0; c < u.length; c++)
                    d.push(l.substring(u[c] + 1, u[c] + 2)),
                    l = l.substring(0, u[c] + 1) + l.substring(u[c] + 2);
                a.data.encrypt_table = d,
                a.data.key_table = [];
                for (var c in a.data.encrypt_table)
                    'q' != a.data.encrypt_table[c] && 'k' != a.data.encrypt_table[c] || (a.data.key_table.push(l.substring(l.length - 12)),
                    l = l.substring(0, l.length - 12));
                a.data.key_table.reverse(),
                a.data.info = l;
                var f = new Array(-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,62,-1,-1,-1,63,52,53,54,55,56,57,58,59,60,61,-1,-1,-1,-1,-1,-1,-1,0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,-1,-1,-1,-1,-1,-1,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,-1,-1,-1,-1,-1);
                a.data.info = function(t) {
                    var e, r, n, i, o, a, s;
                    for (a = t.length,
                    o = 0,
                    s = ''; o < a; ) {
                        do {
                            e = f[255 & t.charCodeAt(o++)]
                        } while (o < a && -1 == e);if (-1 == e)
                            break;
                        do {
                            r = f[255 & t.charCodeAt(o++)]
                        } while (o < a && -1 == r);if (-1 == r)
                            break;
                        s += String.fromCharCode(e << 2 | (48 & r) >> 4);
                        do {
                            if (61 == (n = 255 & t.charCodeAt(o++)))
                                return s;
                            n = f[n]
                        } while (o < a && -1 == n);if (-1 == n)
                            break;
                        s += String.fromCharCode((15 & r) << 4 | (60 & n) >> 2);
                        do {
                            if (61 == (i = 255 & t.charCodeAt(o++)))
                                return s;
                            i = f[i]
                        } while (o < a && -1 == i);if (-1 == i)
                            break;
                        s += String.fromCharCode((3 & n) << 6 | i)
                    }
                    return s
                }(a.data.info);
                for (var c in a.data.encrypt_table) {
                    var h = a.data.encrypt_table[c];
                    if ('q' == h || 'k' == h) {
                        var p = a.data.key_table.pop();
                        a.data.info = s[a.data.encrypt_table[c]](a.data.info, p)
                    } else
                        a.data.info = s[a.data.encrypt_table[c]](a.data.info)
                }
                if (e)
                    return a.data.info;
                var g = '';
                for (c = 0; c < a.data.info.length; c++)
                    g += String.fromCharCode(a.data.info[c]);
                return g
            }
            function Uint8ArrayToString(fileData){
              var dataString = '';
              for (var i = 0; i < fileData.length; i++) {
                dataString += Number(fileData[i]) + ',';
              }
              return dataString;
            }
            function decodeKey(resp){
                var string = eval('('+resp+')');
                //return btoa(String.fromCharCode.apply(null, new Uint8Array(n(string.data.info, 1))));
                return Uint8ArrayToString(new Uint8Array(n(string.data.info, 1)));
            }
            function decodeM3u8(resp){
                var string = eval('('+resp+')');
                return n(string.data.info);
            }
            ";


        public static string DecodeM3u8(string resp)
        {
            var context = new Context();
            context.Eval(JS);
            var concatFunction = context.GetVariable("decodeM3u8").As<Function>();
            string m3u8 = concatFunction.Call(new Arguments { resp }).ToString();
            return m3u8;
        }

        public static string DecodeKey(string resp)
        {
            var context = new Context();
            context.Eval(JS);
            var concatFunction = context.GetVariable("decodeKey").As<Function>();
            string key = concatFunction.Call(new Arguments { resp }).ToString();
            byte[] v = Array.ConvertAll(key.Trim(',').Split(','), s => (byte)int.Parse(s));
            string realKey = Convert.ToBase64String(v);
            return realKey;
        }
    }
}
