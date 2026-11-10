import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import {QQProbe} from './probe.mjs'
function setup(t,replies){const dir=fs.mkdtempSync(path.join(os.tmpdir(),'qq-probe-'));t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));const secret=path.join(dir,'secret.json');fs.writeFileSync(secret,JSON.stringify({webToken:'test-secret'}));const calls=[];const probe=new QQProbe({secret,ownerQQ:'100000001',fetcher:async(url,options)=>{calls.push({url,body:JSON.parse(options.body)});return new Response(JSON.stringify(replies.shift()))}});return {probe,calls}}
test('online flag alone is insufficient; a fresh, read-only native query is required',async t=>{
 const {probe,calls}=setup(t,[{code:0,data:{Credential:'local-only'}},{code:0,data:{isLogin:true}},{code:0,data:{status:'failed',retcode:200,message:'private details'}}])
 const result=await probe.check();assert.equal(result.kind,'failed');assert.ok(!JSON.stringify(result).includes('private details'));assert.equal(calls[2].body.action,'get_stranger_info');assert.equal(calls[2].body.params.no_cache,true)
})
test('logged-out sessions require login and never trigger a QQ write or automated relogin',async t=>{
 const {probe,calls}=setup(t,[{code:0,data:{Credential:'local-only'}},{code:0,data:{isLogin:false}}]);assert.equal((await probe.check()).kind,'needs_login');assert.equal(calls.length,2)
})
test('successful query validates the returned main-account identity',async t=>{
 const {probe}=setup(t,[{code:0,data:{Credential:'local-only'}},{code:0,data:{isLogin:true}},{code:0,data:{status:'ok',retcode:0,data:{user_id:100000001}}}]);assert.equal((await probe.check()).kind,'ok')
})
