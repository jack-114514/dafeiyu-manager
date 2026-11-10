import test from 'node:test'
import assert from 'node:assert/strict'
import {dayKey,planDay,sameSecret,videoId,requireReadOnly,safeRefs} from './core.mjs'
test('Shanghai date does not inherit server timezone',()=>assert.equal(dayKey(Date.parse('2026-11-08T16:01:00Z')),'2026-11-09'))
test('daily windows stay apart and within 20 to 23 Shanghai',()=>{for(const random of [()=>0,()=>.999]){const [a,b]=planDay('2026-11-08',random);assert.ok(a>=Date.parse('2026-11-08T20:00:00+08:00'));assert.ok(b<Date.parse('2026-11-08T23:00:00+08:00'));assert.ok(b-a>=26*60000)}})
test('external writes are rejected',()=>{for(const method of ['POST','DELETE','PATCH','PUT'])assert.throws(()=>requireReadOnly(method));requireReadOnly('GET')})
test('diary references need real read evidence',()=>{const id='BV1xx411c7mD';assert.equal(videoId(id),id);assert.throws(()=>videoId('../../credentials'));assert.throws(()=>safeRefs([id],{}));assert.deepEqual(safeRefs([id],{[id]:{read:true}}),[id])})
test('login token comparison rejects absent and wrong tokens',()=>{assert.equal(sameSecret(null,null),false);assert.equal(sameSecret('a','aa'),false);assert.equal(sameSecret('abc','abc'),true)})
