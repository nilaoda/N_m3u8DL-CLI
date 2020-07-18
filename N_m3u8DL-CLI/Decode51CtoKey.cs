using NiL.JS.BaseLibrary;
using NiL.JS.Core;
using NiL.JS.Extensions;
using System.Security.Cryptography;
using System.Text;

namespace N_m3u8DL_CLI
{
    /*
     * js代码来自：https://static1.51ctocdn.cn/edu/player/h5/h5player.js line:9421
     * 
     */
    class Decode51CtoKey
    {
        private static string JS = @"
var Base64={_keyStr:'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=',encode:function(e){var t='';var n,r,i,s,o,u,a;var f=0;e=Base64._utf8_encode(e);while(f<e.length){n=e.charCodeAt(f++);r=e.charCodeAt(f++);i=e.charCodeAt(f++);s=n>>2;o=(n&3)<<4|r>>4;u=(r&15)<<2|i>>6;a=i&63;if(isNaN(r)){u=a=64}else if(isNaN(i)){a=64}t=t+this._keyStr.charAt(s)+this._keyStr.charAt(o)+this._keyStr.charAt(u)+this._keyStr.charAt(a)}return t},decode:function(e){var t='';var n,r,i;var s,o,u,a;var f=0;e=e.replace(/[^A-Za-z0-9\+\/\=]/g,'');while(f<e.length){s=this._keyStr.indexOf(e.charAt(f++));o=this._keyStr.indexOf(e.charAt(f++));u=this._keyStr.indexOf(e.charAt(f++));a=this._keyStr.indexOf(e.charAt(f++));n=s<<2|o>>4;r=(o&15)<<4|u>>2;i=(u&3)<<6|a;t=t+String.fromCharCode(n);if(u!=64){t=t+String.fromCharCode(r)}if(a!=64){t=t+String.fromCharCode(i)}}t=Base64._utf8_decode(t);return t},_utf8_encode:function(e){e=e.replace(/\r\n/g,'\n');var t='';for(var n=0;n<e.length;n++){var r=e.charCodeAt(n);if(r<128){t+=String.fromCharCode(r)}else if(r>127&&r<2048){t+=String.fromCharCode(r>>6|192);t+=String.fromCharCode(r&63|128)}else{t+=String.fromCharCode(r>>12|224);t+=String.fromCharCode(r>>6&63|128);t+=String.fromCharCode(r&63|128)}}return t},_utf8_decode:function(e){var t='';var n=0;var r=c1=c2=0;while(n<e.length){r=e.charCodeAt(n);if(r<128){t+=String.fromCharCode(r);n++}else if(r>191&&r<224){c2=e.charCodeAt(n+1);t+=String.fromCharCode((r&31)<<6|c2&63);n+=2}else{c2=e.charCodeAt(n+1);c3=e.charCodeAt(n+2);t+=String.fromCharCode((r&15)<<12|(c2&63)<<6|c3&63);n+=3}}return t}}

var btoa = function(str) {
    return Base64.encode(str);
}

var bu = function (e, t) {
    for (var r = t - (e += '').length; 0 < r; r--)
        e = '0' + e;
    return e
}
    ,
    MD5 = function (e) {
        var t, a = 0, n = 8;
        function o(e, t, r, i, a, n) {
            return g((s = g(g(t, e), g(i, n))) << a | s >>> 32 - a, r);
            var s
        }
        function c(e, t, r, i, a, n, s) {
            return o(t & r | ~t & i, e, t, a, n, s)
        }
        function h(e, t, r, i, a, n, s) {
            return o(t & i | r & ~i, e, t, a, n, s)
        }
        function f(e, t, r, i, a, n, s) {
            return o(t ^ r ^ i, e, t, a, n, s)
        }
        function p(e, t, r, i, a, n, s) {
            return o(r ^ (t | ~i), e, t, a, n, s)
        }
        function g(e, t) {
            var r = (65535 & e) + (65535 & t);
            return (e >> 16) + (t >> 16) + (r >> 16) << 16 | 65535 & r
        }
        return function (e) {
            for (var t = a ? '0123456789ABCDEF' : '0123456789abcdef', r = '', i = 0; i < 4 * e.length; i++)
                r += t.charAt(e[i >> 2] >> i % 4 * 8 + 4 & 15) + t.charAt(e[i >> 2] >> i % 4 * 8 & 15);
            return r
        }(function (e, t) {
            e[t >> 5] = e[t >> 5] | (128 << t % 32),
                e[14 + (t + 64 >>> 9 << 4)] = t;
            for (var r = 1732584193, i = -271733879, a = -1732584194, n = 271733878, s = 0; s < e.length; s += 16) {
                var o = r
                    , l = i
                    , u = a
                    , d = n;
                i = p(i = p(i = p(i = p(i = f(i = f(i = f(i = f(i = h(i = h(i = h(i = h(i = c(i = c(i = c(i = c(i, a = c(a, n = c(n, r = c(r, i, a, n, e[s + 0], 7, -680876936), i, a, e[s + 1], 12, -389564586), r, i, e[s + 2], 17, 606105819), n, r, e[s + 3], 22, -1044525330), a = c(a, n = c(n, r = c(r, i, a, n, e[s + 4], 7, -176418897), i, a, e[s + 5], 12, 1200080426), r, i, e[s + 6], 17, -1473231341), n, r, e[s + 7], 22, -45705983), a = c(a, n = c(n, r = c(r, i, a, n, e[s + 8], 7, 1770035416), i, a, e[s + 9], 12, -1958414417), r, i, e[s + 10], 17, -42063), n, r, e[s + 11], 22, -1990404162), a = c(a, n = c(n, r = c(r, i, a, n, e[s + 12], 7, 1804603682), i, a, e[s + 13], 12, -40341101), r, i, e[s + 14], 17, -1502002290), n, r, e[s + 15], 22, 1236535329), a = h(a, n = h(n, r = h(r, i, a, n, e[s + 1], 5, -165796510), i, a, e[s + 6], 9, -1069501632), r, i, e[s + 11], 14, 643717713), n, r, e[s + 0], 20, -373897302), a = h(a, n = h(n, r = h(r, i, a, n, e[s + 5], 5, -701558691), i, a, e[s + 10], 9, 38016083), r, i, e[s + 15], 14, -660478335), n, r, e[s + 4], 20, -405537848), a = h(a, n = h(n, r = h(r, i, a, n, e[s + 9], 5, 568446438), i, a, e[s + 14], 9, -1019803690), r, i, e[s + 3], 14, -187363961), n, r, e[s + 8], 20, 1163531501), a = h(a, n = h(n, r = h(r, i, a, n, e[s + 13], 5, -1444681467), i, a, e[s + 2], 9, -51403784), r, i, e[s + 7], 14, 1735328473), n, r, e[s + 12], 20, -1926607734), a = f(a, n = f(n, r = f(r, i, a, n, e[s + 5], 4, -378558), i, a, e[s + 8], 11, -2022574463), r, i, e[s + 11], 16, 1839030562), n, r, e[s + 14], 23, -35309556), a = f(a, n = f(n, r = f(r, i, a, n, e[s + 1], 4, -1530992060), i, a, e[s + 4], 11, 1272893353), r, i, e[s + 7], 16, -155497632), n, r, e[s + 10], 23, -1094730640), a = f(a, n = f(n, r = f(r, i, a, n, e[s + 13], 4, 681279174), i, a, e[s + 0], 11, -358537222), r, i, e[s + 3], 16, -722521979), n, r, e[s + 6], 23, 76029189), a = f(a, n = f(n, r = f(r, i, a, n, e[s + 9], 4, -640364487), i, a, e[s + 12], 11, -421815835), r, i, e[s + 15], 16, 530742520), n, r, e[s + 2], 23, -995338651), a = p(a, n = p(n, r = p(r, i, a, n, e[s + 0], 6, -198630844), i, a, e[s + 7], 10, 1126891415), r, i, e[s + 14], 15, -1416354905), n, r, e[s + 5], 21, -57434055), a = p(a, n = p(n, r = p(r, i, a, n, e[s + 12], 6, 1700485571), i, a, e[s + 3], 10, -1894986606), r, i, e[s + 10], 15, -1051523), n, r, e[s + 1], 21, -2054922799), a = p(a, n = p(n, r = p(r, i, a, n, e[s + 8], 6, 1873313359), i, a, e[s + 15], 10, -30611744), r, i, e[s + 6], 15, -1560198380), n, r, e[s + 13], 21, 1309151649), a = p(a, n = p(n, r = p(r, i, a, n, e[s + 4], 6, -145523070), i, a, e[s + 11], 10, -1120210379), r, i, e[s + 2], 15, 718787259), n, r, e[s + 9], 21, -343485551),
                    r = g(r, o),
                    i = g(i, l),
                    a = g(a, u),
                    n = g(n, d)
            }
            return Array(r, i, a, n)
        }(function (e) {
            for (var t = Array(), r = (1 << n) - 1, i = 0; i < e.length * n; i += n)
                t[i >> 5] = t[i >> 5] | ((e.charCodeAt(i / n) & r) << i % 32);
            return t
        }(t = e), t.length * n))
    }
    ,
    eeb64 = function (e) {
        for (var t = '', r = '', i = 0; i < e.length; i++)
            t += bu('BqrCwxVefD9457mnoHINOPQRSUXLMabFcdghijyzkl6GApstuJKvW0YZ23ET81=_'.indexOf(e[i]).toString(2), 6);
        for (t = t.substring(t.length % 8),
            i = 0; i < Math.ceil(t.length / 8); i++)
            r += String.fromCharCode(parseInt(t.substr(8 * i, 8), 2));
        return base64decode(r)
    }
    ,
    dec = function (e, t) {
        function r(e) {
            for (var t = 0; t < s.length; t++)
                if (s[t] == e)
                    return t
        }
        e[1];
        var i = [o[r(e[13])], o[r(e[8])], o[r(e[4])]]
            , a = e.substr(0, 1) + e.substr(2, 2) + e.substr(5, 3) + e.substr(9, 4) + e.substr(14);
        debugger;var x = [r(e[13]),r(e[8]),r(e[4])]
        for (var n in i)
            a = i[n](a, t);
        return a
    }
    ,
    base64decode = function (e) {
        var t, r, i, a, n, s, o, l = new Array(-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 62, -1, -1, -1, 63, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, -1, -1, -1, -1, -1, -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, -1, -1, -1, -1, -1);
        for (s = e.length,
            n = 0,
            o = ''; n < s;) {
            for (; t = l[255 & e.charCodeAt(n++)],
                n < s && -1 == t;)
                ;
            if (-1 == t)
                break;
            for (; r = l[255 & e.charCodeAt(n++)],
                n < s && -1 == r;)
                ;
            if (-1 == r)
                break;
            o += String.fromCharCode(t << 2 | (48 & r) >> 4);
            do {
                if (61 == (i = 255 & e.charCodeAt(n++)))
                    return o;
                i = l[i]
            } while (n < s && -1 == i); if (-1 == i)
                break;
            o += String.fromCharCode((15 & r) << 4 | (60 & i) >> 2);
            do {
                if (61 == (a = 255 & e.charCodeAt(n++)))
                    return o;
                a = l[a]
            } while (n < s && -1 == a); if (-1 == a)
                break;
            o += String.fromCharCode((3 & i) << 6 | a)
        }
        return o
    }
    ,
    base64ToArrayBuffer = function (e) {
        for (var t = atob(e), r = t.length, i = new Uint8Array(r), a = 0; a < r; a++)
            i[a] = t.charCodeAt(a);
        return i.buffer
    }
    ,
    arrayBufferToBase64 = function (e) {
        for (var t = '', r = new Uint8Array(e), i = r.byteLength, a = 0; a < i; a++)
            t += String.fromCharCode(r[a]);
        return btoa(t)
    };

var s = ['s', 'i', 'y', 'u', 'a', 'n', 't', 'l', 'w', 'x']
    , o = [function (e) {
        return e
    }
        , function (e, t, r) {
            r = r || 'eDu_51Cto_siyuanTlw';
            for (var i = base64decode(e).split(''), a = MD5(t + r).toString(), n = a.length - 1; 0 <= n; n--) {
                var s = a[n].charCodeAt() % (i.length - 1);
                i.splice(s, 1)
            }
            return i.join('')
        }
        , function (e, t, r) {
            for (var i = t % 7, a = e.length, n = '', s = 0; s < a / 2; s++) {
                var o = 2 * s;
                n += 0 == i || s % i == 0 ? e[o] + e[o + 1] : e[o + 1] ? e[o + 1] + e[o] : e[o]
            }
            var l = base64decode(n)
                , u = (l.length - 1) / 2
                , d = '';
            for (s = 0; s < u; s++)
                o = 2 * s,
                    i < s && o++ ,
                    d += s % 3 == 0 ? l[o] : l[o + 1];
            return d
        }
        , function (e) {
            return e
        }
        , function (e) {
            return e
        }
        , function (e, t, r) {
            var i, a, n, s, o, l, u, d = e.slice(0, 7) + e.slice(10, 12) + e.slice(15, -3), c = '', h = 0, f = 0, p = '';
            d = d.split('').reverse().join(''),
                i = eeb64(d),
                a = parseInt(i.substr(0, 1)),
                s = (n = i.slice(6, -3)).match(/^\d*/),
                o = n.match(/\d*$/),
                l = s[0],
                u = o[0],
                n = n.replace(/^\d*/, '').replace(/\d*$/, '');
            for (var g = 0; g < u.length; g++)
                c += bu(parseInt(u[g]).toString(2), 3);
            for (c = c.substr(a),
                g = 0; g < c.length; g++)
                1 == c[g] ? (p += l[f],
                    f++) : (p += n[h],
                        h++);
            return p
        }
        , function (e, t, r) {
            for (var i, a = {
                B: '0',
                q: '1',
                r: '2',
                C: '3',
                w: '4',
                x: '5',
                V: '6',
                e: '7',
                f: '8',
                D: '9',
                9: 'a',
                4: 'b',
                5: 'c',
                7: 'd',
                m: 'e',
                n: 'f',
                o: 'g',
                H: 'h',
                I: 'i',
                N: 'j',
                O: 'k',
                P: 'l',
                Q: 'm',
                R: 'n',
                S: 'o',
                U: 'p',
                X: 'q',
                L: 'r',
                M: 's',
                a: 't',
                b: 'u',
                F: 'v',
                c: 'w',
                d: 'x',
                g: 'y',
                h: 'z',
                i: 'A',
                j: 'B',
                y: 'C',
                z: 'D',
                k: 'E',
                l: 'F',
                6: 'G',
                G: 'H',
                A: 'I',
                p: 'J',
                s: 'K',
                t: 'L',
                u: 'M',
                J: 'N',
                K: 'O',
                v: 'P',
                W: 'Q',
                0: 'R',
                Y: 'S',
                Z: 'T',
                2: 'U',
                3: 'V',
                E: 'W',
                T: 'X',
                8: 'Y',
                1: 'Z'
            }, n = 5, s = '', o = 0, l = '', u = 0, d = 0; d < e.length; d++) {
                var c = e[d];
                s += a[c] ? a[c] : c
            }
            for (d = 0; d < 8; d++)
                i = 7 == d ? 32 - u : Math.abs(8 - n++),
                    l += s.substr(o++, 1),
                    o += i,
                    u += i;
            return l += s.substr(40),
                eeb64(l.split('').reverse().join(''))
        }
        , function (e, t, r) {
            r = r || 'eDu_51Cto_siyuanTlw';
            var i = eeb64(e)
                , a = MD5(r + t).toString().slice(0, 16)
                , n = i.indexOf(a)
                , s = parseInt(i.slice(0, n), 16);
            if (!n)
                return !1;
            var o = i.substr(16 + n);
            return o.length == s && o
        }
    ];

function getKey(text, lid) {
    return btoa(dec(text, lid));
}";

        private static string MD5Encoding(string rawPass)
        {
            MD5 md5 = MD5.Create();
            byte[] bs = Encoding.UTF8.GetBytes(rawPass);
            byte[] hs = md5.ComputeHash(bs);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hs)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public static string GetDecodeKey(string encodeKey, string lid)
        {
            var context = new Context();
            context.Eval(JS);
            var concatFunction = context.GetVariable("getKey").As<Function>();
            string key = concatFunction.Call(new Arguments { encodeKey, lid }).ToString();
            return key;
        }

        public static string GetSign(string lid)
        {
            var data = lid + "eDu_51Cto_siyuanTlw";
            return MD5Encoding(data);
        }
    }
}
