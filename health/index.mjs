import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import {summarize,scanLog} from './core.mjs'
import {QQProbe} from './probe.mjs'
import {readInstance} from '../instance.mjs'
export function startHealth(ctx){
 const {runtimeDir:ROOT,botQQ,ownerQQ}=readInstance()
 const legacySession='onebot-private-'+ownerQQ
 const state={botQQ,lastInbound:0,lastOutbound:0,running:false,turnStarted:0,turnEnded:0,turnError:false}
 try{const previous=JSON.parse(fs.readFileSync(ROOT+'/health/status.json','utf8'));if(previous.botQQ===state.botQQ){state.lastInbound=Number(previous.lastInbound)||0;state.lastOutbound=Number(previous.lastOutbound)||0}}catch{}
 const aborter=new AbortController(),probe=new QQProbe({signal:aborter.signal});let probing=false,disposed=false
 async function checkSession(){if(probing||disposed)return;probing=true;try{const result=await probe.check();if(!disposed)state.sessionProbe={...result,failures:result.kind==='failed'?(state.sessionProbe?.failures||0)+1:0}}finally{probing=false}}
 const file=ROOT+'/health/status.json';fs.mkdirSync(path.dirname(file),{recursive:true})
 const off=ctx.on('session/event',(session,event)=>{
  if(session.id!==legacySession)return
  if(event.type==='turn/start'){state.running=true;state.turnStarted=Date.now();state.turnError=false}
  if(event.type==='turn/end'){state.running=false;state.turnEnded=Date.now();state.turnError=event.data.reason.kind==='error'}
 })
 function update(){
  let temp
  try{
   const log=ROOT+'/logs/napcat-out.log',fd=fs.openSync(log,'r')
   try{const size=fs.fstatSync(fd).size,buf=Buffer.alloc(Math.min(size,262144));fs.readSync(fd,buf,0,buf.length,Math.max(0,size-buf.length));const times=scanLog(buf.toString('utf8'),new Date().getFullYear(),ownerQQ);const receivedNew=times.lastInbound>state.lastInbound;state.lastInbound=Math.max(state.lastInbound,times.lastInbound);state.lastOutbound=Math.max(state.lastOutbound,times.lastOutbound);if(receivedNew){state.sessionProbe=undefined;void checkSession()}}finally{fs.closeSync(fd)}
   const agent=ctx.agents.get(legacySession);if(agent&&agent.status!=='running')state.running=false
   let adapterError=''
   try{const config=JSON.parse(fs.readFileSync(ROOT+'/life/config.json','utf8'));if(config.fishBridge){const fish=JSON.parse(fs.readFileSync(ROOT+'/life/fish-bridge-status.json','utf8'));if(!fish.installed)adapterError='成长插件 QQ 入口未启动：'+fish.lastError;else{state.lastInbound=Math.max(state.lastInbound,Date.parse(fish.lastReceivedAt)||0);state.lastOutbound=Math.max(state.lastOutbound,Date.parse(fish.lastSentAt)||0);state.running=fish.running===true;state.turnStarted=fish.turnStarted||0;state.turnEnded=fish.turnEnded||0;if(fish.lastError)adapterError='成长插件回复失败：'+fish.lastError}}}catch{}
   temp=file+'.'+crypto.randomUUID()+'.tmp';fs.writeFileSync(temp,JSON.stringify({...state,...summarize(state),...(adapterError?{level:'error',summary:adapterError}:{}),updatedAt:Date.now()}));fs.renameSync(temp,file)
  }catch{/* Monitoring must never take down chat, even when Windows holds a file open. */}
  finally{if(temp)try{fs.unlinkSync(temp)}catch{}}
 }
 update();void checkSession();const timer=setInterval(update,3000),probeTimer=setInterval(checkSession,120000)
 return ()=>{disposed=true;aborter.abort();clearInterval(timer);clearInterval(probeTimer);off()}
}
