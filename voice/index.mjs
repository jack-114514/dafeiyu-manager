import {defineTool} from '@deepseek-ai/dsh-tools'
import {VoiceService} from './core.mjs'
export default {name:'dafeiyu-voice',inject:['tools'],apply(ctx){
 const service=new VoiceService()
 const off=ctx.tools.register(defineTool({name:'dafeiyu_voice',
  description:'大肥鱼 BreezeBlue 正版音色合成。用户要求语音时用 synthesize，将返回的 path 交给 qq_send_voice 发送 QQ 原生语音条；仅合成不会发送。支持 clever 机灵版、dreamy 迷糊版。无密钥、超额或失败就用文字回答，不循环重试。status 只返回非敏感状态。',
  parameters:{action:{type:'string',enum:['status','synthesize'],required:true},text:{type:'string',description:'自然口语正文，最多 300 字；不要写密码、Token 或 API Key。'},voice:{type:'string',enum:['clever','dreamy']}},
  output:{schema:{type:'json'},render:(_args,value)=>[{type:'text',text:JSON.stringify(value)}]},isConcurrencySafe:()=>false,
  execute:async args=>{try{return args.action==='status'?service.status():await service.synthesize(args.text,args.voice)}catch(error){return {ok:false,error:error.message,fallback:'text'}}}
 }));ctx.effect(()=>()=>off())
}}
