import fs from 'node:fs'
import crypto from 'node:crypto'
import path from 'node:path'
import {readInstance} from '../instance.mjs'
// Queries only. Cached get_status is deliberately not treated as a live QQ session check.
export class QQProbe {
 constructor({secret,ownerQQ,fetcher=fetch,signal}={}){
  const instance=secret&&ownerQQ?null:readInstance()
  this.secret=secret??path.join(instance.runtimeDir,'connection-private.json')
  this.ownerQQ=ownerQQ??instance.ownerQQ
  Object.assign(this,{fetcher,signal})
  this.credential=''
 }
 async post(route,body={},authenticated=true){
  if(authenticated&&!this.credential){const key=JSON.parse(fs.readFileSync(this.secret,'utf8').replace(/^\uFEFF/,''));const hash=crypto.createHash('sha256').update(key.webToken+'.napcat').digest('hex');const auth=await this.post('auth/login',{hash},false);if(auth.code!==0||!auth.data?.Credential)throw Error('LOCAL_AUTH');this.credential=auth.data.Credential}
  const response=await this.fetcher('http://127.0.0.1:6100/api/'+route,{method:'POST',redirect:'error',headers:{'content-type':'application/json',...(authenticated?{Authorization:'Bearer '+this.credential}:{})},body:JSON.stringify(body),signal:this.signal?AbortSignal.any([this.signal,AbortSignal.timeout(22000)]):AbortSignal.timeout(22000)})
  if(!response.ok){this.credential='';throw Error('LOCAL_HTTP')}
  const result=await response.json();if(result.code!==0&&authenticated){this.credential='';throw Error('LOCAL_API')};return result
 }
 async check(){
  const start=Date.now()
  try{
   const login=await this.post('QQLogin/CheckLoginStatus')
   if(!login.data?.isLogin)return {kind:'needs_login',checkedAt:Date.now(),rttMs:Date.now()-start}
   const reply=await this.post('Debug/call',{action:'get_stranger_info',params:{user_id:this.ownerQQ,no_cache:true}})
   const good=reply.data?.status==='ok'&&reply.data?.retcode===0&&String(reply.data?.data?.user_id)===this.ownerQQ
   return {kind:good?'ok':'failed',checkedAt:Date.now(),rttMs:Date.now()-start}
  }catch{return {kind:'failed',checkedAt:Date.now(),rttMs:Date.now()-start}}
 }
}
