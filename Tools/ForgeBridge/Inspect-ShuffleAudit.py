"""Inspect one host-private audit; incomplete logs and unusual draws stay explicit."""
import argparse,collections,gzip,hashlib,json,pathlib

def inspect(path):
    digest=hashlib.sha256();header=None;footer=None;records=0;expected_seq=1;errors=[];pending={};identities={};players={};read_error=None
    try:
        with gzip.open(path,'rb')as stream:
            for raw in stream:
                value=json.loads(raw)
                if value.get('type')=='end':
                    if footer is not None:errors.append('Duplicate footer')
                    footer=value;continue
                if footer is not None:errors.append('Record after footer')
                digest.update(raw);records+=1
                if value.get('seq')!=expected_seq:errors.append('Sequence gap at '+str(expected_seq))
                expected_seq+=1
                if value['type']=='start':header=value;continue
                key=(value['gameInstance'],value['seat']);player=players.setdefault(key,dict(shuffles=0,draws=0,emptyOrReplaced=0,unusualDraws=[]))
                known=identities.setdefault(value['gameInstance'],{})
                for card in value.get('identities',[]):known[card['id']]=card
                order=value['library']
                if len(set(order))!=len(order):errors.append('Duplicate physical ID in library at '+str(value['seq']))
                if any(i not in known for i in order):errors.append('Missing card identity at '+str(value['seq']))
                if value['type'].endswith('_before'):pending[value['seq']]=value;continue
                before=pending.pop(value.get('beforeSeq'),None)
                if before is None:errors.append('Unpaired operation at '+str(value['seq']));continue
                if value['type']=='shuffle_after':
                    player['shuffles']+=1
                    if collections.Counter(before['library'])!=collections.Counter(order):errors.append('Shuffle changed physical card multiset at '+str(value['seq']))
                elif value['type']=='draw_after':
                    drawn=value['drawn'];player['draws']+=len(drawn)
                    if not drawn:player['emptyOrReplaced']+=1
                    elif len(drawn)!=1 or drawn[0]['id']!=before['expectedId']:
                        player['unusualDraws'].append(dict(beforeSeq=before['seq'],expectedId=before['expectedId'],drawn=drawn))
    except (OSError,EOFError,ValueError,KeyError)as error:read_error=str(error)
    if footer is None:errors.append('Missing footer')
    else:
        if footer.get('records')!=records:errors.append('Footer record count mismatch')
        if footer.get('recordsSHA256')!=digest.hexdigest():errors.append('Record digest mismatch')
    if pending:errors.append('Unfinished operations: '+str(len(pending)))
    status_path=pathlib.Path(str(path).removesuffix('.partial')+'.status.json');status=None
    if status_path.exists():
        try:status=json.loads(status_path.read_text(encoding='utf8'))
        except (OSError,ValueError)as error:errors.append('Invalid status: '+str(error))
        if status is not None and not status.get('complete'):errors.append('Writer status is incomplete: '+str(status.get('state')))
    complete=not errors and read_error is None and footer is not None and footer.get('complete')is True
    return dict(file=str(path),complete=complete,session=(header or{}).get('session'),roomId=(header or{}).get('roomId'),gameNumber=(header or{}).get('gameNumber'),seed=(header or{}).get('seed'),runtimeHash=(header or{}).get('runtimeHash'),shuffleAlgorithm=(header or{}).get('shuffleAlgorithm'),shufflePasses=(header or{}).get('shufflePasses'),shufflePiles=(header or{}).get('shufflePiles'),shuffleMaxPacket=(header or{}).get('shuffleMaxPacket'),shuffleCuts=(header or{}).get('shuffleCuts'),records=records,reason=(footer or{}).get('reason'),gameFinished=(footer or{}).get('gameFinished',False),errors=errors,readError=read_error,writerError=(footer or{}).get('error'),players={str(k):v for k,v in players.items()},note='Unusual/replaced draws require rule context; they are not automatic shuffle faults.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('file',type=pathlib.Path);parser.add_argument('--output',type=pathlib.Path);args=parser.parse_args()
    result=inspect(args.file);text=json.dumps(result,ensure_ascii=False,indent=2)
    if args.output:args.output.write_text(text,encoding='utf8')
    print(text)
