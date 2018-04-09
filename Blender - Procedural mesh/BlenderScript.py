import bpy
import sys
import random
import mathutils
import math
from mathutils import (Vector, Matrix)

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

class RootParams:

    startPos = Vector((0.0, 0.0 ,0.0))
    startDirection = Vector((1.0, 0.0, 0.0))
    minStep = 1.0
    maxStep = 50.0
    maxLen = 10000.0
    minNoiseDir = -15.0
    maxNoiseDir = 15.0
    splitDir = 30.0
    childRate = 0.15
    levelLenRatio = 0.3
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
    minStep = FloatProperty(name = "Min step", description = "The minimum growth step of each iteration", default = defaultParams.minStep, min = 0.0, max = 100.0)
    maxStep = FloatProperty(name = "Max step", description = "The maximum growth step of each iteration", default = defaultParams.maxStep, min = 0.0, max = 100.0)
    maxLen = FloatProperty(name = "Max Len", description = "The maximum length of a branch of the root", default = defaultParams.maxLen, min = 0.0, max = 100000.0)
    minNoiseDir = FloatProperty(name = "Min dir noise", description = "The minimum noise added to the direction of a branch at each iteration in degrees", default = defaultParams.minNoiseDir, min = -90.0, max = 0.0)
    maxNoiseDir = FloatProperty(name = "Max dir noise", description = "The maximum noise added to the direction of a branch at each iteration in degrees", default = defaultParams.maxNoiseDir, min = 0.0, max = 90.0)
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
        
    h = math.sqrt(normal.z * normal.z + normal.y * normal.y)
    if(h==0.0):
        r1 =Matrix(([1, 0, 0], [0, 1, 0], [0, 0, 1]))
    else:
        r1 = Matrix(([0, normal.z/h, -normal.y/h], [0, normal.y/h, normal.z/h], [1, 0, 0]))
    
    t = r2*r1*s;
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
    
    def clear(self):
        print("clearing list!")
        self.tree = list()
        root = MyTreeNode()
        root.id = 0
        root.parent = 0
        root.maxDiam = sys.float_info.max
        root.level = 1
        root.points.append(Vector(self.params.startPos))
        root.points.append(Vector(self.params.startPos) + Vector(self.params.startDirection))
        root.len = norm(Vector(self.params.startDirection))
        self.tree.append(root)
        self.meshId = 0;
        
        
    def growNode(self, nodeId):
        # bound condition length
        if (self.tree[nodeId].len < (self.params.maxLen * self.params.levelLenRatio ** self.tree[nodeId].level)):
            print("grow node: " + str(nodeId))
            # get current direction add noise and grow by random length
            print("points direction: ", self.tree[nodeId].points[-1], self.tree[nodeId].points[-2])
            d = self.tree[nodeId].points[-1] - self.tree[nodeId].points[-2]
            print("dir = ", d)
            d.normalize()
            print("dir = ", d)
            rd = self.params.minNoiseDir + random.random() * (self.params.maxNoiseDir - self.params.minNoiseDir)
            rs = self.params.minStep + random.random() * (self.params.maxStep - self.params.minStep) #/ min(10.0, max(5, self.tree[nodeId].level*0.5)) #need to review this
            ds = rotate2DZ(d, rd);
            print("rot dir = ", ds)
            ds = ds * rs;
            print("scaled dir = ", ds)
            self.tree[nodeId].points.append((self.tree[nodeId].points[-1] + ds))
            self.tree[nodeId].len = self.tree[nodeId].len + norm(ds)
            print("len: ", self.tree[nodeId].len)
            print("new point: ", self.tree[nodeId].points[-1])
            
            # add children branch 
            if(random.random() < self.params.childRate and self.tree[nodeId].level < self.params.maxLevel and len(self.tree[nodeId].points) > self.params.minNodesBSplit and len(self.tree) < self.params.maxNodes):
                print("new child")
                newNode = MyTreeNode()
                newNode.points.append(self.tree[nodeId].points[-1])
                print("first point: ", newNode.points[0])
                rs = self.params.splitDir + self.params.minNoiseDir + random.random() * (self.params.maxNoiseDir - self.params.minNoiseDir)
                if(random.random() > self.params.LRRate):
                    ds = rotate2DZ(d, rs)
                else:
                    ds = rotate2DZ(d, -rs)
                
                print("dir = ", ds)
                newNode.points.append((newNode.points[-1] + ds))
                print("last point: ", newNode.points[-1])
                print("new node id: ", len(self.tree))
                newNode.len = norm(ds)
                newNode.id = len(self.tree)
                newNode.parent = nodeId
                newNode.startLen = self.tree[nodeId].len
                self.tree.append(newNode)
                self.tree[nodeId].nodes.append(newNode.id)
                
                
    def generateSkeleton(self):
        it = 0
        while(it < self.params.maxIt):
            i = 0
            print("grow node: " + str(i))
            while(i < len(self.tree) and it < self.params.maxIt): #todo multi thread
                self.growNode(i)
                i = i + 1
                it = it + 1
    
    def generateFaces(self, nodeId):
        vertices = list();
        faces = list();
        
        curLen = self.tree[nodeId].len
        parentLen = self.tree[self.tree[nodeId].parent].len - self.tree[nodeId].startLen
        self.tree[nodeId].maxDiam = min( min(curLen, parentLen) * self.params.diamLenScale, self.tree[self.tree[nodeId].parent].maxDiam)
        
        for j in range(0, len(self.tree[nodeId].points) - 1):
            normal = self.tree[nodeId].points[j+1] - self.tree[nodeId].points[j]
            scale = min(curLen * self.params.diamLenScale, self.tree[nodeId].maxDiam)
            ct = transformCircle(self.c, scale, normal, self.tree[nodeId].points[j])
            print(ct)
            vertices.extend(ct)
            assert (curLen >= curLen - norm(normal))
            curLen = curLen - norm(normal)
        
        
        vertices.append(self.tree[nodeId].points[-1])
        
        for j in range(0, len(self.tree[nodeId].points) - 2):
            for i in range(0, self.params.nCircPoints - 2):
                faces.append((j*self.params.nCircPoints + i, j*self.params.nCircPoints + i + 1, (j+1)*self.params.nCircPoints + i + 1, (j+1)*self.params.nCircPoints + i))
            
            faces.append(((j+1)*self.params.nCircPoints - 1, j*self.params.nCircPoints, (j+1)*self.params.nCircPoints, (j+2)*self.params.nCircPoints - 1))
        
        for i in range(0, self.params.nCircPoints - 2):
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
        #ob.show_name = True
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
        print("inside objectc reation")
        print("Len tree" + str(len(self.tree)))
        for i in range(0, len(self.tree)):
            curvedata = bpy.data.curves.new(name='Curve', type='CURVE')
            objectdata = bpy.data.objects.new("ObjCurve", curvedata)
            objectdata.location = (0,0,0) #object origin
            bpy.context.scene.objects.link(objectdata)

            print(i)
            polyline = curvedata.splines.new('POLY')
            polyline.points.add(len(self.tree[i].points)-1)
            print("adding points")
            for j in range(0, len(self.tree[i].points)):
                print(self.tree[i].points[j])
                x, y, z = self.tree[i].points[j]
                polyline.points[j].co = (x, y, z, 1.0)
            #curvedata.update()

            
    def run(self):
        self.clear()
        print("Root elem: ", len(self.tree[0].points))
        self.generateSkeleton()
        self.createLines()
        #for i in range(0, len(self.tree)):
        #    print(i)
        #    vert, fac = self.generateFaces(i)
        #    self.createMesh(vert, fac)
        
# ------------------------------------------------------------------------
#    operators
# ------------------------------------------------------------------------

def testMesh():
    p = list()
    p.append(Vector((1,0,0)))
    p.append(Vector((1,1,0)))
    p.append(Vector((2,2,0)))
    curLen = 5;
    c=makeCircleXZ(12)
    
    vertices = list();
    faces = list()
    for j in range(0, len(p) - 1):
        normal = p[j+1] - p[j]
        scale = min(curLen * 0.05, 10)
        ct = transformCircle(c, scale, normal, p[j])
        vertices.extend(ct)
        curLen = curLen - norm(normal)
        
        
        
    for j in range(0, len(p) - 1):
        for i in range(0, 11):
            faces.append((j*12 + i, j*12 + i + 1, (j+1)*12 + i + 1, (j+1)*12 + i))
        
        faces.append(((j+1)*12 - 1, j*12, (j+1)*12, (j+2)*12 - 1))
    
    # Create mesh and object
    me = bpy.data.meshes.new('my-mesh')
    ob = bpy.data.objects.new('my-mesh', me)
    ob.location = Vector((0,0,0))
    #ob.show_name = True
 
    # Link object to scene and make active
    scn = bpy.context.scene
    scn.objects.link(ob)
    scn.objects.active = ob
    ob.select = True
 
    # Create mesh from given verts, faces.
    me.from_pydata(vertices, [], faces)
    # Update mesh with new data
    me.update()
    #return ob
    
    #for i in range(0, self.params.nCircPoints - 2):
    #    self.faces.append(((len(self.tree[nodeId].points) - 2)* self.params.nCircPoints + i,
    #                       (len(self.tree[nodeId].points) - 2)* self.params.nCircPoints + i + 1,
    #                       (len(self.tree[nodeId].points) - 1)* self.params.nCircPoints))
#    
#    
#    self.faces.append(((len(self.tree[nodeId].points) - 1)* self.params.nCircPoints - 1,
#                       (len(self.tree[nodeId].points) - 2)* self.params.nCircPoints,
#                       (len(self.tree[nodeId].points) - 1)* self.params.nCircPoints))
        

class GeneratorOperator(bpy.types.Operator):
    bl_idname = "wm.root_of_shame_op"
    bl_label = "Generate!"

    def execute(self, context):
        mt = context.scene.my_tool

        curParams = RootParams()
        curParams.startPos = mt.startPos
        curParams.startDirection = mt.startDirection
        curParams.minStep = mt.minStep
        curParams.maxStep = mt.maxStep
        curParams.maxLen = mt.maxLen
        curParams.minNoiseDir = mt.minNoiseDir
        curParams.maxNoiseDir = mt.maxNoiseDir
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
        layout.prop(mt, "minStep")
        layout.prop(mt, "maxStep")
        layout.prop(mt, "maxLen")
        layout.prop(mt, "minNoiseDir")
        layout.prop(mt, "maxNoiseDir")
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