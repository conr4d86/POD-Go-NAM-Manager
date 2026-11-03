"""Observed native and control reservation readers for the 2.50 library.

Only serialized requirements: neither free heap nor execution time is measured.
"""
import struct
from decode_runtime_graphs import decode


class ModuleResources:
    def __init__(self, view, record, catalog):
        self.view=view
        self.base=record['payload_offset']+512
        self.catalog=catalog
        self.by_name={c['name']:c for c in catalog['classes']}
        self.common=self.words(0)

    def data(self,index):
        length,offset=struct.unpack_from('<2I',self.view,self.base+24+8*index)
        return self.view[self.base+4*offset:self.base+4*(offset+length)]

    def words(self,index):
        data=self.data(index)
        return list(struct.unpack('<%dI'%(len(data)//4),data))

    def algorithm(self,asset):
        index=asset-0x600000
        if not 0<=index<self.common[15]:
            raise ValueError('Not a known composite native algorithm: '+hex(asset))
        w=self.common
        e=w[w[16]+7*index:w[16]+7*(index+1)]
        rows=[w[e[3]+20*i:e[3]+20*(i+1)] for i in range(e[2])]
        regions=[w[e[5]+4*i:e[5]+4*(i+1)] for i in range(e[4])]
        return e,rows,regions

    def native_caps(self,asset):
        e,rows,regions=self.algorithm(asset)
        if any(r[0]!=r[5] for r in rows):
            raise ValueError('Aliased native ownership requires separate accounting')
        caps=[sum(r[k] for r in rows) for k in range(13,19)]
        for r in regions:
            caps[:3]=[a+b for a,b in zip(caps[:3],r[1:4])]
        caps[3]+=1
        caps[4]+=e[2]
        caps[5]+=e[4]
        return caps

    @staticmethod
    def control_caps(data):
        w=list(struct.unpack('<%dI'%(len(data)//4),data))
        g=decode(data)['graph']['header_words']
        # SW:16D9DD and 16DB19 allocate these six serialized pools. This includes
        # declared descendant reservations, even for a currently empty modelSlot.
        caps=[g[i] for i in (12,13,23,14,15)]+[g[16]+g[17]]
        for i in range(g[23]):
            p=w[6]+g[10]+8*i
            caps=[a+b for a,b in zip(caps,w[p+2:p+8])]
        return caps

    def model(self,name):
        cls=self.by_name[name]
        index=cls['raw_words'][13]
        w=self.words(index)
        asset=w[w[2]+1]
        e,rows,regions=self.algorithm(asset)
        reads=[r[9] for r in rows if r[4]&4]
        returns=[r[7] for r in rows if r[4]&8]
        inputs=reads[0] if len(reads)==1 else None
        outputs=returns[0] if len(returns)==1 else None
        # These are the SAME stock wrappers selected by their serialized IO
        # shape. The mono wrapper sums L/R before the model; stereo does not.
        if inputs==outputs==1:
            harness_name='P34_AppFxHarnessMono'
        elif inputs==outputs==2:
            harness_name=('P34_AppEqHarnessStereo' if name.startswith('HD2_EQ_STATIC_')
                          else 'P34_AppFxHarnessStereo')
        else:
            harness_name=None
        report={'class_name':name,'class_id':cls['id'],'module_index':index,
                'native_id':asset,'native_descriptor':e,'native_input_channels':inputs,
                'native_output_channels':outputs,'native_reservations':self.native_caps(asset),
                'native_regions':regions,'control_reservations':self.control_caps(self.data(index)),
                'control_bindings':[{k:c[k] for k in ('name','parameter_id','type_word')}
                                    for c in cls['controls']],
                'v3_child_asset_overrides':[w[w[2]+4*i:w[2]+4*i+4]
                    for i in range(1,w[3]) if w[w[2]+4*i+1]!=0xffffffff],
                'harness_class_name':harness_name}
        if harness_name:
            harness=self.by_name[harness_name]
            hw=self.words(harness['raw_words'][13]);ha=hw[hw[2]+1]
            he,hr,_=self.algorithm(ha)
            slot=next(n['raw_words'][4] for n in harness['nodes'] if n['name']=='modelSlot')
            overhead=[sum(r[k] for i,r in enumerate(hr) if i!=slot-1) for k in range(13,19)]
            overhead[3]+=1;overhead[4]+=he[2]
            report.update(harness_module_index=harness['raw_words'][13],harness_native_id=ha,
                          harness_model_slot_object=slot,harness_native_overhead=overhead,
                          wrapped_native_reservations=[a+b for a,b in zip(report['native_reservations'],overhead)])
        return report

