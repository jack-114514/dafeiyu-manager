import crypto from 'node:crypto'
export function dayKey(time=Date.now()) {return new Intl.DateTimeFormat('sv-SE',{timeZone:'Asia/Shanghai',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date(time))}
export function planDay(date, random=Math.random) {
  const start=Date.parse(date+'T20:00:00+08:00')
  return [start+Math.floor(random()*70)*60000,start+(95+Math.floor(random()*80))*60000]
}
export function sameSecret(a,b){if(typeof a!=='string'||typeof b!=='string')return false;const x=Buffer.from(a),y=Buffer.from(b);return x.length===y.length&&crypto.timingSafeEqual(x,y)}
export function videoId(value){if(!/^BV[0-9A-Za-z]{10}$/.test(String(value)))throw Error('视频编号无效');return String(value)}
export function requireReadOnly(method='GET'){if(!['GET','HEAD','OPTIONS'].includes(method.toUpperCase()))throw Error('第一版仅浏览与写本地日记，B 站写操作已关闭')}
export function safeRefs(refs,evidence){return (refs||[]).map(videoId).map(id=>{if(!evidence[id])throw Error('没有读取过该视频的资料，不能记成已经了解：'+id);return id})}
