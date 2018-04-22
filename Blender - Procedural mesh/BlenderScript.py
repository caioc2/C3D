import bpy
import sys
import random
import mathutils
import math
import time
import multiprocessing
from mathutils import (Vector, Matrix)
from threading import Thread

#Add-on header info
bl_info = {
    "name": "RootOfShame - A simple procedural root generator",
    "description": "Creates a simple procedural root-like mesh",
    "author": "Caio SOUZA",
    "version": (0, 0, 1),
    "blender": (2, 79, 4),
    "location": "3D View > Create",
    "warning": "", # used for warning icon and text in addons panel
    "wiki_url": "",
    "tracker_url": "",
    "category": "Development"
}

from bpy.props import (StringProperty,
                       BoolProperty,
                       IntProperty,
                       FloatProperty,
                       FloatVectorProperty,
                       EnumProperty,
                       PointerProperty,
                       )
from bpy.types import (Panel,
                       Operator,
                       PropertyGroup,
                       )

# ------------------------------------------------------------------------
#    store properties in the active scene
# ------------------------------------------------------------------------

bpy.context.scene.render.engine = "CYCLES"
if(bpy.data.objects.get('Lamp') is not None):
    bpy.data.objects['Lamp'].data.type = "SUN"

class RootParams:
    
    startPos = Vector((0.0, 0.0 ,0.0))
    startDirection = Vector((1.0, 0.0, 0.0))
    meanStep = 0.03
    varStep = 0.005
    maxLen = 10000.0
    meanNoiseDir = 0
    varNoiseDir = 10.0
    splitDir = 30.0
    childRate = 0.15
    levelLenRatio = 0.7
    LRRate = 0.5
    diamLenScale = 0.01
    maxLevel = 5
    minNodesBSplit = 5
    maxNodes = 500
    maxIt = 5500
    nCircPoints = 50
    override = True
    
defaultParams = RootParams()

class RootSettings(PropertyGroup):

    startPos = FloatVectorProperty(name="Start Pos", description="The starting position to create the root", default = defaultParams.startPos)
    startDirection = FloatVectorProperty(name="Start Dir", description="The starting direction to create the root", default = defaultParams.startDirection)
    meanStep = FloatProperty(name = "Mean step", description = "The minimum growth step of each iteration", default = defaultParams.meanStep, min = 0.0, max = 100.0)
    varStep = FloatProperty(name = "Var step", description = "The maximum growth step of each iteration", default = defaultParams.varStep, min = 0.0, max = 100.0)
    maxLen = FloatProperty(name = "Max Len", description = "The maximum length of a branch of the root", default = defaultParams.maxLen, min = 0.0, max = 100000.0)
    meanNoiseDir = FloatProperty(name = "Mean dir noise", description = "The minimum noise added to the direction of a branch at each iteration in degrees", default = defaultParams.meanNoiseDir, min = -90.0, max = 90.0)
    varNoiseDir = FloatProperty(name = "Var dir noise", description = "The maximum noise added to the direction of a branch at each iteration in degrees", default = defaultParams.varNoiseDir, min = 0.0, max = 90.0)
    splitDir = FloatProperty(name = "Branch Dir", description = "The angle formed by a branching in the root", default = defaultParams.splitDir, min = 0.0, max = 180.0)
    childRate = FloatProperty(name = "Branch rate", description = "The rate of branch per iteraction", default = defaultParams.childRate, min = 0.0, max = 1.0)
    levelLenRatio = FloatProperty(name = "Level\length ratio", description = "The maximum ratio of the length between a branch and its child", default = defaultParams.levelLenRatio, min = 0.0, max = 1.0)
    LRRate = FloatProperty(name = "LR rate", description = "The bias (left-right) of the branches (0.5 = neutral, 0.0 = only left, 1.0 = only right", default = defaultParams.LRRate, min = 0.0, max = 1.0)
    diamLenScale = FloatProperty(name = "Diam\length ratio", description = "The scale of the branch diameter based on its length", default = defaultParams.diamLenScale, min = 0.00000001, max = 0.1)
    maxLevel = IntProperty(name = "Max Level", description = "The maximum level of branching", default = defaultParams.maxLevel, min = 2, max = 1000)
    minNodesBSplit = IntProperty(name = "Min nodes Bsplit", description = "Min nodes one branch shall have before being able to split", default = defaultParams.minNodesBSplit, min = 1, max = 1000)
    maxNodes = IntProperty(name = "Max nodes (branches)", description = "The maximum # of nodes (branches) when generating the root", default = defaultParams.maxNodes, min = 1, max = 1000)
    maxIt = IntProperty(name = "Max Iteractions", description = "The maximum iteractions to generate the root", default = defaultParams.maxIt, min = 10, max = 10000000)
    nCircPoints = IntProperty(name = "# Circle points", description = "The # of points to discretize the circle", default = defaultParams.nCircPoints, min = 3, max = 200)
    override = BoolProperty(name = "Override", description = "Override the last generated mesh", default = defaultParams.override)
    tree = []
    
class MyTreeNode:
    
    def __init__(self):
        self.id = -1 #node id (array indexing)
        self.nodes = list()#child nodes
        self.parent = -1
        self.points = list() # 3d points (vectors)
        self.len = 0.0
        self.startLen = 0.0
        self.maxDiam = 0.0
        self.level = 0
    
def norm(v):
    return math.sqrt(v.x*v.x + v.y*v.y + v.z*v.z)

def rotate2DZ(v, angle):
    angle = math.pi * angle / 180.0
    vr = Vector((0.0, 0.0, v.z))
    c = math.cos(angle)
    s = math.sin(angle)
    vr.x = c * v.x - s * v.y
    vr.y = s * v.x + c * v.y
    return vr

def makeCircleXZ(nPts):
    c = list()
    for i in range(0, nPts):
        a = 2 * math.pi * i/nPts
        c.append(Vector((math.cos(a), 0, math.sin(a))))
    
    return c
    
      
def transformCircle(circlePoints, scale, normal, pos):
    s = Matrix(([scale, 0, 0], [0, scale, 0], [0, 0, scale]))
    h = math.sqrt(normal.x * normal.x + normal.y * normal.y)
    if(h==0.0):
        r2 =Matrix(([1, 0, 0], [0, 1, 0], [0, 0, 1]))
    else:
        r2 = Matrix(([normal.y/h, normal.x/h, 0], [-normal.x/h, normal.y/h, 0], [0, 0, 1]))
    
    t = r2*s;
    newCircle = list();
    for i in range(0, len(circlePoints)):
        v = t*circlePoints[i] + pos
        newCircle.append(v)
    
    return newCircle 
 
class MyRootTree:

    def __init__(self, params):
        self.tree = list()
        self.params = params
        self.c = makeCircleXZ(self.params.nCircPoints)
        self.clear()
        self.coreCount = 1# multiprocessing.cpu_count() #no improvement so far
    
    def clear(self):
        self.tree = list()
        root = MyTreeNode()
        root.id = 0
        root.parent = 0
        root.maxDiam = sys.float_info.max
        root.level = 1
        root.points.append(Vector(self.params.startPos))
        root.points.append(Vector(self.params.startPos) + Vector(self.params.startDirection) * self.params.meanStep)
        root.len = norm(Vector(self.params.startDirection) * self.params.meanStep)
        self.tree.append(root)
        self.meshId = 0;
        
        
    def growNode(self, nodeId):
        # bound condition length
        if (self.tree[nodeId].len < (self.params.maxLen * self.params.levelLenRatio ** self.tree[nodeId].level)):
            # get current direction add noise and grow by random length
            d = self.tree[nodeId].points[-1] - self.tree[nodeId].points[-2]
            d.normalize()
            la = random.normalvariate(0,1) 
            lb = random.normalvariate(0,1)
            rd = self.params.meanNoiseDir + la * self.params.varNoiseDir
            rs = self.params.meanStep +  lb * self.params.varStep
            ds = rotate2DZ(d, rd);
            ds = (ds * rs)*(self.params.levelLenRatio**(self.tree[nodeId].level -1));
            self.tree[nodeId].points.append((self.tree[nodeId].points[-1] + ds))
            self.tree[nodeId].len = self.tree[nodeId].len + norm(ds)
            
            # add children branch 
            if(random.random() < self.params.childRate and self.tree[nodeId].level < self.params.maxLevel and len(self.tree[nodeId].points) > self.params.minNodesBSplit and len(self.tree) < self.params.maxNodes):

                newNode = MyTreeNode()
                newNode.points.append(self.tree[nodeId].points[-1])
                rs = self.params.splitDir + self.params.meanNoiseDir + random.normalvariate(0,1) * self.params.varNoiseDir
                d = d * self.params.meanStep
                if(random.random() > self.params.LRRate):
                    ds = rotate2DZ(d, rs)
                else:
                    ds = rotate2DZ(d, -rs)
                
                newNode.points.append((newNode.points[-1] + ds))
                newNode.len = norm(ds)
                newNode.id = len(self.tree)
                newNode.parent = nodeId
                newNode.startLen = self.tree[nodeId].len
                newNode.level = self.tree[nodeId].level + 1
                self.tree.append(newNode)
                self.tree[nodeId].nodes.append(newNode.id)
                
                
    def generateSkeleton(self):
        it = 0
        while(it < self.params.maxIt):
            i = 0
            while(i < len(self.tree) and it < self.params.maxIt):
                self.growNode(i)
                i = i + 1
                it = it + 1
    
    def generateFaces(self, nodeId):
        vertices = list()
        faces = list()
        
        curLen = self.tree[nodeId].len
        parentLen = self.tree[self.tree[nodeId].parent].len - self.tree[nodeId].startLen
        self.tree[nodeId].maxDiam = min( min(curLen, parentLen) * self.params.diamLenScale, self.tree[self.tree[nodeId].parent].maxDiam)
        
        lastNormal = self.tree[nodeId].points[1] - self.tree[nodeId].points[0];
        for j in range(0, len(self.tree[nodeId].points) - 1):
            normal = self.tree[nodeId].points[j+1] - self.tree[nodeId].points[j]
            dir = lastNormal + normal;
            scale = min(curLen * self.params.diamLenScale, self.tree[nodeId].maxDiam)
            ct = transformCircle(self.c, scale, dir, self.tree[nodeId].points[j])
            vertices.extend(ct)
            curLen = curLen - norm(normal)
            lastNormal = normal
        
        
        vertices.append(self.tree[nodeId].points[-1])
        
        for j in range(0, len(self.tree[nodeId].points) - 2):
            for i in range(0, self.params.nCircPoints - 1):
                faces.append((j*self.params.nCircPoints + i, j*self.params.nCircPoints + i + 1, (j+1)*self.params.nCircPoints + i +1, (j+1)*self.params.nCircPoints + i))
            
            faces.append(((j+1)*self.params.nCircPoints - 1, j*self.params.nCircPoints, (j+1)*self.params.nCircPoints, (j+2)*self.params.nCircPoints - 1))
        
        for i in range(0, self.params.nCircPoints - 1):
            faces.append(((len(self.tree[nodeId].points) - 2)* self.params.nCircPoints + i,
                               (len(self.tree[nodeId].points) - 2)* self.params.nCircPoints + i + 1,
                               (len(self.tree[nodeId].points) - 1)* self.params.nCircPoints))
        
        
        faces.append(((len(self.tree[nodeId].points) - 1)* self.params.nCircPoints - 1,
                           (len(self.tree[nodeId].points) - 2)* self.params.nCircPoints,
                           (len(self.tree[nodeId].points) - 1)* self.params.nCircPoints))
        
        return vertices, faces
        
    def createMesh(self, vertices, faces):
        # Create mesh and object
        me = bpy.data.meshes.new('mesh'+str(self.meshId)+'-root_of_shame')
        ob = bpy.data.objects.new(str(self.meshId)+'-root_of_shame', me)
        ob.location = self.params.startPos
        ob.show_name = True
        self.meshId = self.meshId+1
     
        # Link object to scene and make active
        scn = bpy.context.scene
        scn.objects.link(ob)
        scn.objects.active = ob
        ob.select = True
     
        # Create mesh from given verts, faces.
        me.from_pydata(vertices, [], faces)
        # Update mesh with new data
        me.update()
        return ob

    def createLines(self):
        for i in range(0, len(self.tree)):
            curvedata = bpy.data.curves.new(name='Curve', type='CURVE')
            objectdata = bpy.data.objects.new("ObjCurve", curvedata)
            objectdata.location = (0,0,0) #object origin
            bpy.context.scene.objects.link(objectdata)

            polyline = curvedata.splines.new('POLY')
            polyline.points.add(len(self.tree[i].points)-1)
            for j in range(0, len(self.tree[i].points)):
                x, y, z = self.tree[i].points[j]
                polyline.points[j].co = (x, y, z, 1.0)
            #curvedata.update()
        
    def threadGenFaces(self, startIdx):
        print("Running on thread: ", startIdx)
        for i in range(startIdx, len(self.tree), self.coreCount):
            vert, fac = self.generateFaces(i)
            self.createMesh(vert, fac)

            
    def run(self):
        self.clear()
        t = time.time()
        if(self.params.override):
            for item in bpy.data.meshes:
                bpy.data.meshes.remove(item)
        t2 = time.time()
        print("\nTime taken clearing old meshes: ", t2 - t)
        
        self.generateSkeleton()
        t3 = time.time()
        print("Time taken generating skeleton: ", t3 - t2)
        #self.createLines()
        
        threads = list()
        for i in range(0, self.coreCount):
            threads.append(Thread(target = self.threadGenFaces, args = (i, )))
            threads[i].start()
        
        for i in range(0, self.coreCount):
            threads[i].join();
            
        bpy.ops.group.create()
        t4 = time.time()
        print("Time taken creating vertices and faces: ", t4 - t3)
        print("Total time: ", t4 - t)
        
# ------------------------------------------------------------------------
#    operators
# ------------------------------------------------------------------------      

class GeneratorOperator(bpy.types.Operator):
    bl_idname = "wm.root_of_shame_op"
    bl_label = "Generate!"

    def execute(self, context):
        mt = context.scene.my_tool

        curParams = RootParams()
        curParams.startPos = mt.startPos
        curParams.startDirection = mt.startDirection
        curParams.meanStep = mt.meanStep
        curParams.varStep = mt.varStep
        curParams.maxLen = mt.maxLen
        curParams.meanNoiseDir = mt.meanNoiseDir
        curParams.varNoiseDir = mt.varNoiseDir
        curParams.splitDir = mt.splitDir
        curParams.childRate = mt.childRate
        curParams.levelLenRatio = mt.levelLenRatio
        curParams.LRRate = mt.LRRate
        curParams.diamLenScale = mt.diamLenScale
        curParams.maxLevel = mt.maxLevel
        curParams.minNodesBSplit = mt.minNodesBSplit
        curParams.maxNodes = mt.maxNodes
        curParams.maxIt = mt.maxIt
        curParams.nCircPoints = mt.nCircPoints
        curParams.override = mt.override
        
        genRoot = MyRootTree(curParams)
        genRoot.run()
        #testMesh()
        #mt.tree.append(genRoot)
        return {'FINISHED'}

# ------------------------------------------------------------------------
#    rootOfShame tool in objectmode
# ------------------------------------------------------------------------

class OBJECT_PT_root_of_shame_panel(Panel):
    bl_idname = "OBJECT_PT_root_of_shame"
    bl_label = "RootOfShame - Mesh Generator"
    bl_space_type = "VIEW_3D"   
    bl_region_type = "TOOLS"    
    bl_category = "Create"
    bl_context = "objectmode"

    def draw(self, context):
        layout = self.layout
        mt = context.scene.my_tool

        layout.prop(mt, "startPos")
        layout.prop(mt, "startDirection")
        layout.prop(mt, "meanStep")
        layout.prop(mt, "varStep")
        layout.prop(mt, "maxLen")
        layout.prop(mt, "meanNoiseDir")
        layout.prop(mt, "varNoiseDir")
        layout.prop(mt, "splitDir")
        layout.prop(mt, "childRate")
        layout.prop(mt, "levelLenRatio")
        layout.prop(mt, "LRRate")
        layout.prop(mt, "diamLenScale")
        layout.prop(mt, "maxLevel")
        layout.prop(mt, "minNodesBSplit")
        layout.prop(mt, "maxNodes")
        layout.prop(mt, "maxIt")
        layout.prop(mt, "nCircPoints")
        layout.prop(mt, "override")
        layout.operator("wm.root_of_shame_op")

 

# ------------------------------------------------------------------------
# register and unregister
# ------------------------------------------------------------------------

def register():
    bpy.utils.register_module(__name__)
    bpy.types.Scene.my_tool = PointerProperty(type=RootSettings)

def unregister():
    bpy.utils.unregister_module(__name__)
    del bpy.types.Scene.my_tool

if __name__ == "__main__":
    register()