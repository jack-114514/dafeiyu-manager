import {VoiceService} from './core.mjs'
const service=new VoiceService()
try{
 const result=process.argv[2]==='sample'?await service.synthesize('你好呀，我是大肥鱼，现在可以用语音和你聊天啦。'):service.status()
 console.log(JSON.stringify(result))
}catch(error){console.log(JSON.stringify({ok:false,error:error.message}));process.exitCode=1}
