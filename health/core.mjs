export function summarize(state,now=Date.now()){
 const input=state.lastInbound||0,output=state.lastOutbound||0,pending=input>output
 const probe=state.sessionProbe
 if(probe&&now-probe.checkedAt<300000){
  if(probe.kind==='needs_login')return {level:'error',summary:'服务器 QQ 授权已失效或未登录，请重新确认登录'}
  if(probe.kind==='failed'&&probe.failures>=2)return {level:'error',summary:'服务器 QQ 主动查询连续失败，会话可能已失效'}
 }
 if(state.turnError&&state.turnEnded>=input&&pending)return {level:'error',summary:'模型请求失败，尚未回复'}
 if(state.running)return now-state.turnStarted>120000?{level:'error',summary:'模型生成超过两分钟，请检查'}:{level:'working',summary:'正在生成回复'}
 if(pending&&now-input>30000)return {level:'error',summary:state.turnEnded>=input?'回复尚未送达':'已收消息，处理未开始'}
 if(pending)return {level:'working',summary:'已收到消息，等待回复'}
 if(output&&now-output<600000)return {level:'verified',summary:'最近有收发成功记录'}
 return {level:'unverified',summary:probe?.kind==='ok'&&now-probe.checkedAt<300000?'QQ 主动查询通过，收发仍需新消息实测':'连接存在，收发待实测'}
}
export function scanLog(text,year,ownerQQ){
 let lastInbound=0,lastOutbound=0
 for(const line of text.replace(/\x1b\[[0-9;]*m/g,'').split('\n')){
  const m=line.match(/^(\d{2}-\d{2}) (\d{2}:\d{2}:\d{2}).*? (接收 <-|发送 ->) 私聊 \((\d+)\)/)
  if(!m||m[4]!==ownerQQ)continue
  const at=Date.parse(year+'-'+m[1]+'T'+m[2]+'+08:00');if(!Number.isFinite(at))continue
  if(m[3]==='接收 <-')lastInbound=Math.max(lastInbound,at);else lastOutbound=Math.max(lastOutbound,at)
 }
 return {lastInbound,lastOutbound}
}
