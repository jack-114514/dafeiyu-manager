import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'

export const VOICES={clever:'voc_u3nxkd5578wc',dreamy:'voc_8yud7hz7c9q2'}
export const DELIVERY={
 clever:{instructions:'像一只机灵爱撒娇的小鲸鱼，见到熟悉的人回来，有点得意地想分享小秘密。声音清亮靠前，开头“诶”轻巧地扬起，中间语速稍快，“快听我说嘛”带着笑、略微拖一个俏皮的尾音。情绪变化鲜明，像正在面对面说话，不要播音、低声耳语或压低嗓音。',voice_settings:{guidance_scale:3.5,speed:1.08}},
 dreamy:{instructions:'用轻盈清亮、柔和放松的声音和亲近的人聊天，停顿自然，语调有温柔的小起伏。不压低嗓音，不用低沉的耳语或平直播音腔。',voice_settings:{guidance_scale:2,speed:1}}
}
import {privateDir,readInstance} from '../instance.mjs'
export const SETTINGS=path.join(privateDir,'breeze-tts.json')
export const defaults={enabled:true,voiceId:VOICES.clever,maxChars:300,dailyChars:3000,apiKey:''}
const day=()=>new Intl.DateTimeFormat('sv-SE',{timeZone:'Asia/Shanghai',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())
export function read(file,fallback={}){try{return JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''))}catch{return fallback}}
function write(file,value){fs.mkdirSync(path.dirname(file),{recursive:true});const temp=file+'.tmp';fs.writeFileSync(temp,JSON.stringify(value,null,2));fs.renameSync(temp,file)}
export class VoiceService {
 constructor({settings=SETTINGS,media,usage,fetcher=fetch,deliveryProfiles=DELIVERY}={}){
  const runtime=media&&usage?null:readInstance().runtimeDir
  this.media=media??path.join(runtime,'media','voice')
  this.usage=usage??path.join(runtime,'voice','usage.json')
  Object.assign(this,{settings,fetcher,deliveryProfiles})
  this.busy=false
 }
 config(){const cfg={...defaults,...read(this.settings)};cfg.apiKey=String(cfg.apiKey||'').trim();return cfg}
 used(){const u=read(this.usage);return u.date===day()?u:{date:day(),chars:0,calls:0}}
 status(){const c=this.config(),u=this.used();return {enabled:c.enabled===true,keyConfigured:!!c.apiKey,voiceId:c.voiceId,voiceName:c.voiceId===VOICES.dreamy?'大肥鱼（迷糊版）':'大肥鱼（机灵版）',maxChars:c.maxChars,dailyChars:c.dailyChars,requestedCharsToday:u.chars,requestsToday:u.calls,ready:c.enabled===true&&!!c.apiKey}}
 async synthesize(text,voice){
  if(this.busy)throw Error('语音正在合成，请等上一条完成。')
  const c=this.config();if(!c.enabled)throw Error('语音已关闭，请用文字回复。')
  if(!c.apiKey)throw Error('尚未填写 BreezeBlue API Key，请在大肥鱼管家 → 大肥鱼语音中填写；暂用文字回复。')
  const voiceId=voice?VOICES[voice]:c.voiceId
  if(!Object.values(VOICES).includes(voiceId))throw Error('只能选择大肥鱼机灵版或迷糊版。')
  text=String(text||'').trim();const chars=[...text].length
  const max=Math.max(1,Math.min(1000,Number(c.maxChars)||300))
  if(chars<1||chars>max)throw Error('语音正文需在 1–'+max+' 字之间，请先精简口语表达。')
  const delivery=this.deliveryProfiles[voiceId===VOICES.clever?'clever':'dreamy']
  // Delivery belongs to the identity: a new performance must never replay an old flat cache entry.
  const key=crypto.createHash('sha256').update(JSON.stringify({voiceId,text,delivery,format:'mp3_24000_96'})).digest('hex')
  const file=path.join(this.media,key+'.mp3')
  if(fs.existsSync(file)&&fs.statSync(file).size>100)return {ok:true,path:file,voiceId,chars,cached:true,sent:false,next:'调用 qq_send_voice，path 使用本文件；收到 sent:true 后才算发送成功。'}
  const u=this.used(),budget=Math.max(1,Math.min(100000,Number(c.dailyChars)||3000))
  if(u.chars+chars>budget)throw Error('今天语音申请字符已达本地上限，请用文字回复。')
  this.busy=true
  try{
   // Reserve before sending. A timeout may still have consumed upstream credits; never auto-retry.
   write(this.usage,{date:day(),chars:u.chars+chars,calls:u.calls+1})
   let response
   try{response=await this.fetcher('https://api.breeze.blue/v1/text-to-speech/'+voiceId+'?output_format=mp3_24000_96&delivery=sync',{
    method:'POST',headers:{'xi-api-key':c.apiKey,'content-type':'application/json',accept:'audio/mpeg'},redirect:'error',
    body:JSON.stringify({text,language_code:'zh',...delivery}),signal:AbortSignal.timeout(90000)
   })}catch{throw Error('BreezeBlue 网络请求未完成，未自动重试；本次改用文字回复。')}
   if(!response.ok){await response.body?.cancel();const hint={401:'API Key 无效或已失效',402:'额度不足',403:'此账号暂不能使用该音色',404:'音色暂不可用',429:'请求过多或并发已满'}[response.status]||'服务暂不可用';throw Error('语音合成失败：'+hint+'（HTTP '+response.status+'），本次改用文字。')}
   if(!/^audio\/(mpeg|mp3)/i.test(response.headers.get('content-type')||'')){await response.body?.cancel();throw Error('服务没有返回 MP3 音频，本次改用文字。')}
   const chunks=[];let total=0
   for await(const chunk of response.body){total+=chunk.length;if(total>12*1024*1024){await response.body.cancel().catch(()=>{});throw Error('语音文件超过大小上限。')}chunks.push(chunk)}
   const audio=Buffer.concat(chunks)
   if(audio.length<100||!(audio.subarray(0,3).toString()==='ID3'||(audio[0]===255&&(audio[1]&224)===224)))throw Error('语音音频格式校验失败。')
   fs.mkdirSync(this.media,{recursive:true});fs.writeFileSync(file+'.tmp',audio);fs.renameSync(file+'.tmp',file)
   return {ok:true,path:file,voiceId,chars,cached:false,sent:false,next:'调用 qq_send_voice，path 使用本文件；收到 sent:true 后才算发送成功。'}
  }finally{this.busy=false}
 }
}
