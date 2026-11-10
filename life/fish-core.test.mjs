import test from 'node:test'
import assert from 'node:assert/strict'
import {dayKey,projectPath,shareAllowed} from './fish-core.mjs'
test('Chinese date determines the quota reset',()=>assert.equal(dayKey(new Date('2026-11-08T16:01:00Z')),'2026-11-09'))
test('project paths cannot traverse or write executable scripts',()=>{for(const name of ['../keys.txt','x/../../a.md','C:/key.txt','a.ps1','.git/config','x\\a.md'])assert.throws(()=>projectPath('C:/home/projects',name));assert.ok(projectPath('C:/home/projects','story/one.md'))})
test('proactive quota counts pending and uncertain sends and requires real events',()=>{const now=new Date('2026-11-08T13:00:00Z'),events=[{id:'real',type:'utterance'}],args={text:'发现了一件新鲜事',refs:['real']};assert.equal(shareAllowed(args,events,[],now).state,'pending');assert.throws(()=>shareAllowed({...args,refs:['fake']},events,[],now));assert.throws(()=>shareAllowed(args,events,Array.from({length:5},()=>({day:'2026-11-08',state:'uncertain'})),now));assert.throws(()=>shareAllowed(args,events,[{day:'2026-11-08',text:args.text}],now));assert.ok(shareAllowed(args,events,[{day:'2026-11-07',text:args.text}],now))})
