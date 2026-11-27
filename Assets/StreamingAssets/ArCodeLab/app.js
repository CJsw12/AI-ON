'use strict';
const $=s=>document.querySelector(s), stage=$('#stage');
let cameraRun=0, starting=false;
let state={}, lastSignature='', connected=false, sourceVideo=null, media=null, arContext=null, marker=null, arMode=false, closed=false;
const scene=new THREE.Scene(), root=new THREE.Group(); scene.add(root);
let camera=new THREE.PerspectiveCamera(45,4/3,.01,100);camera.position.set(0,3.8,4.2);camera.lookAt(0,0,0);
const renderer=new THREE.WebGLRenderer({alpha:true,antialias:true});renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.setClearColor(0x12192a,1);stage.appendChild(renderer.domElement);
scene.add(new THREE.HemisphereLight(0xffffff,0x334466,1.6));
const boxes=[];
function label(text){const c=document.createElement('canvas');c.width=512;c.height=256;const x=c.getContext('2d');x.fillStyle='#17243c';x.fillRect(0,0,512,256);x.fillStyle='#fff';x.textAlign='center';x.font='bold 52px sans-serif';String(text).split('\n').forEach((v,i)=>{x.font=i===1?'bold 90px sans-serif':'bold 56px sans-serif';x.fillText(v,256,85+i*110);});return new THREE.CanvasTexture(c);}
function tile(text,x,z,color){const group=new THREE.Group();const box=new THREE.Mesh(new THREE.BoxGeometry(1.25,.5,.9),new THREE.MeshLambertMaterial({color}));group.add(box);const panel=new THREE.Mesh(new THREE.PlaneGeometry(1.18,.59),new THREE.MeshBasicMaterial({map:label(text),side:THREE.DoubleSide}));panel.rotation.x=-Math.PI/2;panel.position.y=.258;group.add(panel);group.position.set(x,.15,z);root.add(group);return {group,box,panel};}
boxes.push(tile('합계\n0',-1.5,0,0x8ec9ff),tile('현재 점수\n—',0,0,0xff8fb1),tile('평균\n—',1.5,0,0x8fdcc0));
const array=[4,8,12].map((v,i)=>tile('점수['+i+']\n'+v,(i-1)*1.5,-1.5,0x8d9abc));
const pointer=new THREE.Mesh(new THREE.ConeGeometry(.15,.35,12),new THREE.MeshBasicMaterial({color:0xffe08a}));root.add(pointer);pointer.position.y=.9;
function resize(){renderer.setSize(stage.clientWidth,stage.clientHeight,false);if(!arMode){camera.aspect=stage.clientWidth/stage.clientHeight;camera.updateProjectionMatrix();}}
window.addEventListener('resize',resize);resize();
function setLabel(tile,text){const old=tile.panel.material.map;tile.panel.material.map=label(text);old.dispose();tile.panel.material.needsUpdate=true;}
function display(s){
 const lines=['sum = 0;','foreach (var score in scores) {',s.repair?'  // 초기화는 반복문 밖에서 한 번만':'  sum = 0;  // 여기가 원인?','  sum += score;','} average = sum / scores.Length;'];lines[2]=s.repair?'  // 초기화는 반복문 밖에서 한 번만':lines[2];
 $('#code').replaceChildren(...lines.map((text,i)=>{const row=document.createElement('span');row.className='line'+(s.line===i?' active':'');row.textContent=(i+1)+'  '+text;return row;}));
 $('#sum').textContent='합계 '+s.sum;$('#value').textContent='현재 점수 '+(s.index>=0?s.value:'—');$('#average').textContent='평균 '+s.average;
 $('#note').textContent=s.note;$('#professor').textContent=s.professor+' · 변수 추적 실습';
 $('#guide').textContent=s.instruction;$('#play').textContent=s.automatic?'일시 정지':'자동 실행';$('#play').disabled=!s.started||s.finished||s.done;
 $('#repair').textContent=s.repair?'원래 오류로 비교':'초기화 위치 수정';
 $('#phase').textContent=s.done?'실습이 끝났습니다. PC에서 결과를 확인하세요.':s.started?'실습 중 · 남은 시간 '+s.left+'초':'준비 · 시작 전에는 시간이 줄지 않습니다.';
 $('#start').disabled=s.started||s.done;$('#step').disabled=!s.started||s.finished||s.done;
 document.querySelectorAll('[data-command="reset"],[data-command="repair"]').forEach(b=>b.disabled=s.done||(b.dataset.command==='repair'&&!s.canRepair));
 $('#ai').textContent=s.explanation||'현재 실행 상태에 관해 질문해 보세요.';$('#source').textContent=s.aiBusy?'AI 서버에 질문 중…':s.aiSource;$('#ask').disabled=s.aiBusy||s.done;
 const signature=[s.sum,s.index,s.value,s.average,s.repair,s.line].join('/');
 if(signature!==lastSignature){lastSignature=signature;setLabel(boxes[0],'합계\n'+s.sum);setLabel(boxes[1],'현재 점수\n'+(s.index>=0?s.value:'—'));setLabel(boxes[2],'평균\n'+s.average);array.forEach((v,i)=>v.box.material.color.setHex(i===s.index?0xffe08a:0x8d9abc));pointer.visible=s.index>=0;pointer.position.x=(s.index-1)*1.5;pointer.position.z=-1.5;}
}
async function command(name){if(!connected)return;const id=Date.now().toString(36)+Math.random().toString(36).slice(2);try{const r=await fetch('action?name='+encodeURIComponent(name)+'&id='+id,{cache:'no-store'});if(!r.ok)throw Error('전송 실패');}catch{$('#error').textContent='명령을 보내지 못했습니다. PC 연결을 확인한 후 다시 눌러 주세요.';}}
document.querySelectorAll('[data-command]').forEach(b=>b.onclick=()=>command(b.dataset.command));
$('form').onsubmit=e=>{e.preventDefault();const value=$('#question').value.trim();if(value){command('ask:'+value);$('#question').value='';}};
async function refresh(){if(closed)return;try{const r=await fetch('state',{cache:'no-store'});if(!r.ok)throw Error();state=await r.json();if(!('sum' in state))throw Error();connected=true;$('#network').textContent='● PC와 연결됨 · 동일한 실행 상태';display(state);}catch{connected=false;$('#network').textContent='PC 연결이 끊겼습니다. 실습 화면과 Wi-Fi를 확인하세요.';}setTimeout(refresh,500);}
async function tlsInfo(){try{const r=await fetch('config');const url=await r.text();if(url){const a=document.createElement('a');a.className='btn';a.href=url;a.textContent='HTTPS로 열기';const cert=document.createElement('a');cert.className='btn alt';cert.href='root.cer';cert.textContent='로컬 인증서 받기';$('#tlslinks').replaceChildren(cert,a);}else setTimeout(tlsInfo,1500);}catch{}}
function stopCamera(){cameraRun++;starting=false;$('#camera').disabled=false;arMode=false;root.scale.setScalar(1);if(media)media.getTracks().forEach(t=>t.stop());media=null;if(sourceVideo)sourceVideo.remove();sourceVideo=null;if(arContext&&arContext.arController)arContext.arController.dispose();arContext=null;if(marker){scene.remove(marker);marker=null;}root.visible=true;scene.add(root);camera=new THREE.PerspectiveCamera(45,4/3,.01,100);camera.position.set(0,3.8,4.2);camera.lookAt(0,0,0);renderer.setClearColor(0x12192a,1);$('#mode').textContent='3D 미리보기 · 카메라 사용 없음';$('#tracking').textContent='합계·현재 점수·평균이 PC와 함께 바뀝니다.';resize();}
$('#preview').onclick=stopCamera;
$('#camera').onclick=async()=>{
 if(!window.isSecureContext||!navigator.mediaDevices){$('#tls').hidden=false;$('#error').textContent='현재 HTTP 주소에서는 휴대폰 카메라를 사용할 수 없습니다. 아래 HTTPS 연결 안내를 확인해 주세요.';tlsInfo();return;}
 if(arMode||starting)return;
 const generation=++cameraRun;starting=true;$('#camera').disabled=true;
 $('#error').textContent='카메라 연결 중…';
 try{
 const acquired=await navigator.mediaDevices.getUserMedia({audio:false,video:{facingMode:{ideal:'environment'},width:{ideal:640},height:{ideal:480}}});
 if(generation!==cameraRun){acquired.getTracks().forEach(t=>t.stop());return;}
 media=acquired;
 sourceVideo=document.createElement('video');sourceVideo.muted=true;sourceVideo.autoplay=true;sourceVideo.playsInline=true;sourceVideo.srcObject=media;stage.prepend(sourceVideo);await sourceVideo.play();if(generation!==cameraRun)return;
 arMode=true;camera=new THREE.Camera();scene.add(camera);marker=new THREE.Group();scene.add(marker);marker.add(root);root.scale.setScalar(.48);
 arContext=new THREEx.ArToolkitContext({cameraParametersUrl:'camera_para.dat',detectionMode:'mono',maxDetectionRate:30});
 const pendingContext=arContext;
 const initTimeout=setTimeout(()=>{if(generation===cameraRun&&starting){stopCamera();$('#error').textContent='AR 추적 초기화 시간이 초과되었습니다. 페이지를 다시 열어 주세요.';}},15000);
 arContext.init(()=>{clearTimeout(initTimeout);if(generation!==cameraRun){if(pendingContext.arController)pendingContext.arController.dispose();return;}camera.projectionMatrix.copy(pendingContext.getProjectionMatrix());new THREEx.ArMarkerControls(pendingContext,marker,{type:'pattern',patternUrl:'patt.hiro',changeMatrixMode:'modelViewMatrix'});starting=false;$('#camera').disabled=false;$('#mode').textContent='카메라 AR · 학습 카드 추적';});
 renderer.setClearColor(0x000000,0);$('#mode').textContent='카메라 AR 준비 중…';$('#error').textContent='';resize();
 }catch(error){if(generation!==cameraRun)return;stopCamera();$('#error').textContent='카메라를 시작하지 못했습니다. Safari/Chrome의 카메라 권한과 HTTPS 인증서 신뢰 상태를 확인하세요.';}
};
function animate(){if(closed)return;requestAnimationFrame(animate);if(arMode&&arContext&&arContext.arController&&sourceVideo){arContext.update(sourceVideo);root.visible=marker.visible;$('#tracking').textContent=marker.visible?'학습 카드 인식됨 · 카드를 움직이면 변수 상자가 따라갑니다.':'학습 카드의 검은 테두리 전체가 보이도록 비춰 주세요.';}renderer.render(scene,camera);}
window.addEventListener('pagehide',()=>{closed=true;stopCamera();renderer.dispose();});
window.addEventListener('pageshow',e=>{if(e.persisted)location.reload();});
refresh();animate();
