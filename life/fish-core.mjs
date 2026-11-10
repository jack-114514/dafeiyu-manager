import path from 'node:path'
export function dayKey(now=new Date()){return new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Shanghai',year:'numeric',month:'2-digit',day:'2-digit'}).format(now)}
export function projectPath(root,name){
 if(typeof name!=='string'||!name||name.includes(':')||name.includes('\\')||name.split('/').some(x=>!x||x==='..'||x.startsWith('.')))throw Error('只允许作品目录内的普通文件')
 const file=path.resolve(root,name),base=path.resolve(root)+path.sep
 if(!file.startsWith(base)||!['.md','.txt','.json','.html','.css','.js'].includes(path.extname(file).toLowerCase()))throw Error('不支持此文件位置或类型')
 return file
}
export function shareAllowed({text,refs},events,entries,now=new Date()){
 if(!String(text||'').trim()||String(text).length>600)throw Error('分享正文需要 1–600 字')
 if(!Array.isArray(refs)||!refs.length||refs.length>6)throw Error('需要引用 1–6 条实际成长事件')
 const material=new Set(events.filter(e=>['utterance','revise','reunion','narration','refusal','heard'].includes(e.type)).map(e=>e.id))
 if(refs.some(id=>!material.has(id)))throw Error('引用的真实事件不存在')
 const today=entries.filter(e=>e.day===dayKey(now))
 // Pending and uncertain deliveries reserve slots too: never retry ambiguous sends.
 if(today.length>=5)throw Error('今天主动消息已达到五条上限')
 if(today.some(e=>e.text===String(text).trim()))throw Error('这条分享今天已经发送或尝试过')
 return {day:dayKey(now),text:String(text).trim(),refs,at:now.toISOString(),state:'pending'}
}
