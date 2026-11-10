import test from 'node:test'
import assert from 'node:assert/strict'
import {summarize,scanLog} from './core.mjs'
test('TCP alone never proves replies; pending, model and delivery failures are distinct',()=>{
 const now=1000000;assert.equal(summarize({},now).level,'unverified')
 assert.equal(summarize({lastInbound:now-31000},now).summary,'已收消息，处理未开始')
 assert.equal(summarize({lastInbound:now-31000,turnEnded:now-1000},now).summary,'回复尚未送达')
 assert.equal(summarize({lastInbound:now-31000,turnEnded:now-1000,turnError:true},now).level,'error')
 assert.equal(summarize({running:true,turnStarted:now-121000},now).level,'error')
 assert.equal(summarize({lastInbound:now-5000,lastOutbound:now-1000},now).level,'verified')
 assert.equal(summarize({lastOutbound:now-700000},now).level,'unverified')
})
test('log reader uses the main QQ account only and stores timestamps, not chat text',()=>{
 const r=scanLog('11-08 15:48:01 [info] 蓝色大肥鱼 | 接收 <- 私聊 (100000001) 收到请回复测试\n11-08 15:48:03 [info] 蓝色大肥鱼 | 发送 -> 私聊 (100000001) 收到\n11-08 15:49:03 [info] 蓝色大肥鱼 | 发送 -> 私聊 (1234) irrelevant',2026,'100000001')
 assert.equal(r.lastInbound,Date.parse('2026-11-08T15:48:01+08:00'));assert.equal(r.lastOutbound,Date.parse('2026-11-08T15:48:03+08:00'));assert.equal(Object.keys(r).length,2)
})
test('a live session failure overrides cached online and historical delivery; one transient failure is tolerated',()=>{
 const now=1000000,recent={lastInbound:now-5000,lastOutbound:now-1000}
 assert.equal(summarize({...recent,sessionProbe:{kind:'failed',checkedAt:now,failures:1}},now).level,'verified')
 assert.equal(summarize({...recent,sessionProbe:{kind:'failed',checkedAt:now,failures:2}},now).level,'error')
 assert.equal(summarize({...recent,sessionProbe:{kind:'needs_login',checkedAt:now}},now).level,'error')
 assert.equal(summarize({sessionProbe:{kind:'ok',checkedAt:now}},now).level,'unverified')
})
