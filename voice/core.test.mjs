import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import {VoiceService,VOICES,DELIVERY} from './core.mjs'
function setup(t,config={},fetcher){const root=fs.mkdtempSync(path.join(os.tmpdir(),'fish-voice-'));t.after(()=>fs.rmSync(root,{recursive:true,force:true}));const settings=path.join(root,'settings.json');fs.writeFileSync(settings,JSON.stringify({apiKey:'test-api-key-placeholder',...config}));return new VoiceService({settings,usage:path.join(root,'usage.json'),media:path.join(root,'media'),fetcher})}
const audio=()=>Buffer.concat([Buffer.from('ID3'),Buffer.alloc(150)])
test('official API contract, both exact voice IDs, cache, no key disclosure',async t=>{
 let calls=0;const service=setup(t,{},async(url,options)=>{calls++;assert.match(url,/^https:\/\/api.breeze.blue\/v1\/text-to-speech\/voc_/);assert.equal(options.headers['xi-api-key'],'test-api-key-placeholder');assert.equal(options.redirect,'error');assert.equal(JSON.parse(options.body).language_code,'zh');return new Response(audio(),{headers:{'content-type':'audio/mpeg'}})})
 for(const voice of ['clever','dreamy']){const r=await service.synthesize('你好呀',voice);assert.equal(r.voiceId,VOICES[voice]);assert.equal(r.sent,false);assert.ok(fs.existsSync(r.path))}
 assert.equal((await service.synthesize('你好呀','clever')).cached,true);assert.equal(calls,2);assert.ok(!JSON.stringify(service.status()).includes('test-api-key-placeholder'))
})
test('missing key, disabled, input size and local budget do not call API',async t=>{
 const denied=()=>{throw Error('unexpected network')}
 await assert.rejects(setup(t,{apiKey:''},denied).synthesize('你好'),/API Key/)
 await assert.rejects(setup(t,{enabled:false},denied).synthesize('你好'),/关闭/)
 await assert.rejects(setup(t,{maxChars:2},denied).synthesize('你好呀'),/精简/)
 await assert.rejects(setup(t,{dailyChars:1},denied).synthesize('你好'),/本地上限/)
})
test('no automatic paid retries or secret-bearing server errors; failed audio not saved',async t=>{
 let calls=0;const service=setup(t,{},async()=>{calls++;return new Response('test-api-key-placeholder',{status:401})})
 await assert.rejects(service.synthesize('你好'),e=>!e.message.includes('test-api-key-placeholder')&&e.message.includes('401'));assert.equal(calls,1)
 const invalid=setup(t,{},async()=>new Response('not mp3',{headers:{'content-type':'audio/mpeg'}}));await assert.rejects(invalid.synthesize('你好'),/格式校验/);assert.ok(!fs.existsSync(invalid.media))
})
test('concurrent synthesis rejected; budget reserved on ambiguous network failure',async t=>{
 let finish;const service=setup(t,{},()=>new Promise(resolve=>{finish=resolve}));const first=service.synthesize('你好')
 await assert.rejects(service.synthesize('你好'),/正在合成/);finish(new Response(audio(),{headers:{'content-type':'audio/mpeg'}}));await first
 const failed=setup(t,{},async()=>{throw Error('private network details')});await assert.rejects(failed.synthesize('你好'),/未自动重试/);assert.equal(failed.status().requestedCharsToday,2)
})
test('expressive delivery is sent to Breeze and invalidates old flat cached audio',async t=>{
 const crypto=await import('node:crypto');let body
 const service=setup(t,{},async(url,options)=>{body=JSON.parse(options.body);return new Response(audio(),{headers:{'content-type':'audio/mpeg'}})})
 fs.mkdirSync(service.media,{recursive:true});const old=path.join(service.media,crypto.createHash('sha256').update(VOICES.clever+'\n你好呀').digest('hex')+'.mp3');fs.writeFileSync(old,audio())
 const result=await service.synthesize('你好呀','clever');assert.equal(result.cached,false);assert.notEqual(result.path,old);assert.equal(body.instructions,DELIVERY.clever.instructions);assert.equal(body.voice_settings.speed,1.08);assert.equal(body.voice_settings.guidance_scale,3.5)
 assert.equal((await service.synthesize('你好呀','clever')).cached,true)
})
test('two audition performances of the same text keep distinct cache files',async t=>{
 let calls=0;const service=setup(t,{},async()=>{calls++;return new Response(audio(),{headers:{'content-type':'audio/mpeg'}})})
 const first=await service.synthesize('诶，你回来啦！','clever')
 service.deliveryProfiles={...DELIVERY,clever:{instructions:'更活泼地说',voice_settings:{guidance_scale:3,speed:1.12}}}
 const second=await service.synthesize('诶，你回来啦！','clever');assert.notEqual(first.path,second.path);assert.equal(calls,2)
 assert.equal((await service.synthesize('诶，你回来啦！','clever')).cached,true)
})
