"""Generate the original AutoTwin EV-01 concept, then export its Unity FBX.

blender -b --factory-startup --python Tools/Blender/build_ev01.py -- [output.fbx] [output.blend]
Design coordinates are X right, Y up, Z forward. Blender stores them as (X, Z, Y).
"""

from collections import defaultdict
from math import cos, exp, pi, sin, sqrt
from pathlib import Path
import sys

import bpy
from mathutils import Vector


COLORS = {
    "Paint": (.11, .157, .184), "Glass": (.013, .024, .038),
    "Seal": (.018, .025, .034), "Graphite": (.034, .052, .068),
    "Satin": (.35, .48, .55), "Alloy": (.62, .72, .76),
    "Tire": (.026, .034, .042), "Tread": (.018, .023, .031),
    "Ice": (.48, .92, 1.0), "Tail": (.95, .036, .09),
    "HV": (1., .36, .044), "Battery": (.42, .64, .70),
    "Coolant": (.055, .57, .69), "Copper": (.67, .30, .091),
    "Lens": (.06, .095, .137), "Cabin": (.135, .155, .176),
    "Liner": (.021, .028, .035), "Display": (.048, .116, .161),
}


def lerp(a, b, t):
    return a + (b - a) * t


def steps(a, b, count):
    return [lerp(a, b, i / count) for i in range(count + 1)]


def spline(keys, x):
    # Cubic Hermite interpolation of the longitudinal styling stations.
    values = list(keys)
    if x <= values[0][0]:
        return values[0][1]
    if x >= values[-1][0]:
        return values[-1][1]
    i = next(i for i in range(len(values) - 1) if values[i + 1][0] >= x)
    a, b = values[i], values[i + 1]
    p, q = values[max(i - 1, 0)], values[min(i + 2, len(values) - 1)]
    width = b[0] - a[0]
    t = (x - a[0]) / width
    t2, t3 = t * t, t * t * t
    return ((2*t3 - 3*t2 + 1) * a[1] + (t3 - 2*t2 + t) * width * (b[1]-p[1])/(b[0]-p[0])
            + (-2*t3 + 3*t2) * b[1] + (t3-t2) * width * (q[1]-a[1])/(q[0]-a[0]))


def as_blender(point):
    return point[0], point[2], point[1]


class Component:
    def __init__(self, name, root, origin=(0, 0, 0)):
        self.name = name
        self.origin = Vector(origin)
        self.root = bpy.data.objects.new(name, None)
        self.root.parent = root
        self.root.location = as_blender(origin)
        bpy.context.collection.objects.link(self.root)
        self.geometry = defaultdict(lambda: ([], []))

    def grid(self, material, us, vs, surface, outward):
        verts, faces = self.geometry[material]
        start = len(verts)
        verts.extend(Vector(surface(u, v)) for u in us for v in vs)
        row = len(vs)
        for i in range(len(us) - 1):
            for j in range(row - 1):
                a = start + i * row + j
                face = (a, a + row, a + row + 1, a + 1)
                p, q, r, s = (verts[k] for k in face)
                normal = (q - p).cross(s - p)
                if normal.length_squared < 1e-15:
                    normal = (r - q).cross(s - q)
                direction = outward((p + q + r + s) * .25) if callable(outward) else Vector(outward)
                # Swapping Y and Z mirrors winding when writing Blender coordinates.
                faces.append(tuple(reversed(face)) if normal.dot(direction) >= 0 else face)

    def tube(self, material, points, radius, sides=8):
        path = [Vector(p) for p in points]
        frames = []
        for i, point in enumerate(path):
            tangent = (path[min(len(path)-1, i+1)] - path[max(0, i-1)]).normalized()
            guide = Vector((0, 1, 0)) if abs(tangent.y) < .94 else Vector((1, 0, 0))
            u = tangent.cross(guide).normalized()
            v = tangent.cross(u).normalized()
            frames.append((point, u, v))

        verts, faces = self.geometry[material]
        start = len(verts)
        for point, u, v in frames:
            verts.extend(point + radius * (u * cos(k*2*pi/sides) + v * sin(k*2*pi/sides)) for k in range(sides))
        for i in range(len(path)-1):
            for k in range(sides):
                a = start + i*sides + k
                b = start + i*sides + (k+1) % sides
                faces.append((a, a+sides, b+sides, b))
        faces.append(tuple(start + k for k in range(sides)))
        faces.append(tuple(start + (len(path)-1)*sides + k for k in reversed(range(sides))))

    def rounded_box(self, material, center, sizes, radius=.035, yaw=0):
        dimensions = [value * .5 for value in sizes]
        radius = min(radius, min(dimensions) * .92)
        inset = [value - radius for value in dimensions]

        def orient(p):
            return Vector((cos(yaw)*p[0]+sin(yaw)*p[2], p[1], -sin(yaw)*p[0]+cos(yaw)*p[2]))

        def stations(half):
            return [-half, -half+.31*radius, -half+.68*radius, -half+radius,
                    half-radius, half-.68*radius, half-.31*radius, half]

        for axis in range(3):
            j, k = [a for a in range(3) if a != axis]
            for sign in (1, -1):
                def rounded(u, v):
                    p = Vector((0, 0, 0))
                    p[axis], p[j], p[k] = sign*dimensions[axis], u, v
                    core = Vector(tuple(max(-inset[i], min(inset[i], p[i])) for i in range(3)))
                    return Vector(center) + orient(core + radius * (p - core).normalized())

                normal = Vector((0, 0, 0))
                normal[axis] = sign
                self.grid(material, stations(dimensions[j]), stations(dimensions[k]), rounded, orient(normal))

    def lathe(self, material, center, profile, count=80):
        # Revolve a cross section around the local vehicle X axis.
        for i in range(len(profile) - 1):
            a, b = profile[i], profile[i+1]
            tangent_x, tangent_r = b[0] - a[0], b[1] - a[1]
            self.grid(material, steps(0, 2*pi, count), (0, 1),
                      lambda phi, t: (center[0]+lerp(a[0], b[0], t), center[1]+lerp(a[1], b[1], t)*cos(phi),
                                      center[2]+lerp(a[1], b[1], t)*sin(phi)),
                      lambda p: Vector((-tangent_r, 0, 0)) + tangent_x * Vector((0, p.y-center[1], p.z-center[2])).normalized())

    def finish(self):
        for label, (verts, faces) in self.geometry.items():
            name = f"{label}__{self.name}"
            mesh = bpy.data.meshes.new(name)
            mesh.from_pydata([as_blender(p - self.origin) for p in verts], [], faces)
            mesh.update()
            # Weld the parametric station boundaries before Blender exports smooth normals.
            import bmesh
            edit = bmesh.new()
            edit.from_mesh(mesh)
            bmesh.ops.remove_doubles(edit, verts=list(edit.verts), dist=.00001)
            edit.to_mesh(mesh)
            edit.free()
            mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
            obj = bpy.data.objects.new(name, mesh)
            obj.parent = self.root
            obj.data.materials.append(bpy.data.materials[label])
            bpy.context.collection.objects.link(obj)


WHEEL_Y = .373
AXLE = 1.445
WHEEL_X = .818
FRONT = 2.389
REAR = -2.389
REAR_SHOULDER = -2.164
DOOR_BACK = -1.035
DOOR_SPLIT = -.17
DOOR_FRONT = .785
SILL = .388

BWIDTH = ((REAR,.728),(-2.16,.856),(-1.86,.916),(-1.47,.944),(-1.06,.932),
          (-.49,.902),(.35,.91),(.82,.916),(1.41,.938),(1.88,.913),(2.13,.851),(FRONT,.747))
BELT = ((REAR,.908),(-2.14,.945),(-1.63,1.006),(-1.05,1.025),(-.45,1.018),
        (.37,1.002),(.82,1.006),(1.34,.983),(1.89,.904),(2.14,.845),(FRONT,.783))


def belt(z):
    return spline(BELT, z)


def width(z):
    return spline(BWIDTH, z)


def wheel_arch(z, radius=.409):
    d = abs(abs(z)-AXLE)
    return WHEEL_Y + sqrt(radius*radius-d*d) if d < radius else WHEEL_Y


def lower(z):
    return max(SILL, wheel_arch(z))


def flank(side, z, y):
    t = (belt(z) - y) / (belt(z) - SILL)
    convex = .068*sin(pi*max(0, min(1, .85*t))) - .109*max(0, t)**2
    crease = .031*exp(-((t-.82)/.19)**2) * exp(-(z/.96)**4)
    roll = .016*exp(-((abs(z)-AXLE)/.29)**2)*sin(pi*max(0, min(1, t)))
    return side*(width(z)+convex-crease+roll), y, z


def cabin_upper(z):
    r = spline(((-1.68,.77),(-1.30,.71),(-.96,.66),(-.48,.627),(.12,.61),(.50,.605),(.93,.758)), z)
    y = spline(((-1.68,1.006),(-1.30,1.30),(-.96,1.486),(-.56,1.539),(.04,1.543),(.49,1.493),(.93,1.048)), z)
    return r, y


def greenhouse(side, z, t):
    high_x, high_y = cabin_upper(z)
    low_y = belt(z) + .018
    low_x = width(z) - .045
    return side*(lerp(low_x, high_x, t)+.018*sin(pi*t)), lerp(low_y, high_y, t), z


def roof_surface(z, u):
    x, y = cabin_upper(z)
    return x*u, y + .073*(1-u*u), z


def hatch_surface(u, t):
    top = roof_surface(-.94 + .095*u*u, u)
    z = REAR + .037 + .18*abs(u)**4
    bottom = (u * width(z)*.979, belt(z)+.009, z)
    # Large raked rear screen meeting the pronounced rear deck and tail signature.
    return (lerp(top[0], bottom[0], t), lerp(top[1], bottom[1], t)+.12*sin(pi*t),
            lerp(top[2], bottom[2], t)-.021*sin(pi*t))


def nose_station(u):
    z = FRONT - .011 - .227*abs(u)**4
    return u*width(z), belt(z)+.023*(1-u*u), z


def hood_surface(u, t):
    x, y, z = nose_station(u)
    back = .84 + .055*u*u
    station = lerp(back, z, t)
    wide = lerp(.855, abs(nose_station(1)[0]), t)
    x = u*wide
    base = belt(station) + .042*(1-u*u) - .018*exp(-((abs(u)-.67)/.16)**2) * sin(pi*t)
    return x, lerp(base, y, t**6), station


def screen(u, t):
    x, y, z = roof_surface(.49 + .017*u*u, u)
    return (lerp(u*.817, x, t), lerp(1.044+.028*(1-u*u), y, t)+.023*sin(pi*t),
            lerp(.916+.028*(1-u*u), z, t)+.029*sin(pi*t)*(1-u*u))


def trace(component, material, function, lo, hi, count, radius):
    component.tube(material, [function(u) for u in steps(lo, hi, count)], radius)


def roof_glazing(body):
    # 从同一条曲面取样，玻璃和车漆相接时不重叠，也不出现悬浮封边。
    front, back, edge = .460, -.890, .903
    long = steps(back, front, 40)
    body.grid("Glass", long, steps(-edge, edge, 32), roof_surface, (0,1,0))
    for side in (-1, 1):
        body.grid("Paint",long,steps(edge,1,6),lambda z,u:roof_surface(z,side*u),(0,1,0))
        trace(body,"Seal",lambda z:roof_surface(z,side*edge),back,front,40,.003)
    for start, end in ((-.935,back),(front,.493)):
        body.grid("Paint",steps(start,end,6),steps(-1,1,32),roof_surface,(0,1,0))
    for z in (back,front):
        trace(body,"Seal",lambda u:roof_surface(z,u),-edge,edge,30,.003)


def wheel_house(body, side, axle):
    # 内衬桥接翼子板与内轮罩，悬架与卡钳固定在车身层级。
    def inner(angle, depth):
        radius = .422 + .022*depth
        z = axle + radius*cos(angle)
        y = WHEEL_Y + radius*sin(angle)
        outer_x = abs(flank(side,z,y)[0])-.012
        return side*lerp(outer_x,.632,depth),y,z

    body.grid("Liner",steps(.025,pi-.025,52),steps(0,1,9),inner,
              lambda p:(0,WHEEL_Y-p.y,axle-p.z))
    side_wall = lambda a,k: (side*.631,WHEEL_Y+lerp(.333,.445,k)*sin(a),axle+lerp(.333,.445,k)*cos(a))
    body.grid("Liner",steps(.04,pi-.04,46),steps(0,1,5),side_wall,(side,0,0))
    body.tube("Satin",[(side*.745,.47,axle-.032),(side*.642,.758,axle+.014)],.036,16)
    trace(body,"Graphite",lambda a:(side*(.661+.048*cos(a)-.085*a/(pi*12)),
        .548+.195*a/(pi*12),axle+.056*sin(a)),0,12*pi,108,.009)
    for offset in (-.232,.198):
        body.tube("Graphite",[(side*.486,.277,axle+offset),(side*.619,.284,axle+offset*.7),
            (side*.727,.331,axle+offset*.2)],.036,12)
    body.tube("Satin",[(side*.514,.444,axle+.25),(side*.738,.397,axle+.142)],.018,12)


def dashboard_cabin(body):
    body.rounded_box("Cabin",(0,.884,.635),(1.51,.144,.343),.066)
    body.rounded_box("Seal",(0,.921,.562),(1.47,.032,.087),.012)
    body.rounded_box("Satin",(0,.925,.519),(1.43,.008,.010),.003)
    body.rounded_box("Graphite",(.048,.977,.49),(.321,.208,.038),.012)
    body.rounded_box("Display",(.048,.981,.468),(.303,.185,.006),.0026)
    body.rounded_box("Satin",(0,.711,.047),(.194,.024,.398),.011)
    for z in (-.032,.10):
        trace(body,"Seal",lambda a:(.050*cos(a),.725,z+.047*sin(a)),0,2*pi,24,.006)
    for side in (-1,1):
        body.rounded_box("Liner",(side*.435,.488,.33),(.55,.021,.41),.008)
        body.rounded_box("Graphite",(side*.425,.545,.775),(.262,.074,.342),.03)
    body.tube("Satin",[(-.385,.834,.49),(-.385,.857,.411)],.033,16)
    for side in (-1,1):
        body.tube("Seal",[(-.385,.861,.412),(-.385+side*.117,.853,.408)],.026,12)
    body.rounded_box("Cabin",(-.385,.86,.393),(.112,.086,.049),.024)


def bodywork(root):
    body = Component("Body", root)
    roof_glazing(body)
    body.grid("Glass", steps(-.973,.973,38), steps(0,1,20), screen, (0,1,1))
    for t in (0,1):
        trace(body, "Seal", lambda u: screen(u,t), -1, 1, 30, .010)
    for side in (-1, 1):
        trace(body, "Paint", lambda t: screen(side,t), 0, 1, 24, .017)
        trace(body, "Paint", lambda z: roof_surface(z,side), -.929, .497, 36, .010)
        trace(body, "Seal", lambda z: greenhouse(side,z,1), -1.77,.84,42,.012)
        for a, b in ((REAR_SHOULDER,DOOR_BACK),(DOOR_FRONT,2.11)):
            body.grid("Paint", steps(a,b,48), steps(0,1,16),
                      lambda z,t: flank(side,z,lerp(lower(z),belt(z),t)), (side,0,0))
        body.grid("Graphite", steps(DOOR_BACK,DOOR_FRONT,35), steps(0,1,8),
                  lambda z,t: flank(side,z,lerp(.306,.391,t)), (side,0,0))
        trace(body, "Satin", lambda z: flank(side,z,.35), DOOR_BACK+.035, DOOR_FRONT-.023,34,.012)
        for axle in (-AXLE,AXLE):
            wheel_house(body,side,axle)
            body.grid("Seal", steps(.03,pi-.03,44), steps(.002,.036,5),
                      lambda angle,lift: (
                          flank(side,axle+(.408+lift)*cos(angle), WHEEL_Y+(.408+lift)*sin(angle))[0]+side*.008,
                          WHEEL_Y+(.408+lift)*sin(angle), axle+(.408+lift)*cos(angle)), (side,0,0))
            caliper_angle = -.78
            body.rounded_box("Copper", (side*(WHEEL_X+.107),WHEEL_Y+.195*cos(caliper_angle),
                                        axle+.195*sin(caliper_angle)),(.047,.094,.059),.018)
        # The front shoulder rolls from the side swage to the separate bonnet.
        body.grid("Paint", steps(.012,.996,36), steps(0,1,8),
                  lambda t,k: tuple(lerp(h, f, k) for h,f in zip(
                      hood_surface(side*.991,t), flank(side,hood_surface(side,t)[2],belt(hood_surface(side,t)[2])))), (0,1,0))
        body.grid("Paint", steps(0,1,24), steps(0,1,5),
                  lambda t,k: tuple(lerp(h,f,k) for h,f in zip(hatch_surface(side,t),
                      flank(side,hatch_surface(side,t)[2],belt(hatch_surface(side,t)[2])))), (0,1,0))

        # Cabin pillars and firm interior silhouettes remain part of the coachwork.
        trace(body, "Graphite", lambda t: greenhouse(side,DOOR_SPLIT,t), 0,1,13,.037)
        trace(body, "Graphite", lambda t: greenhouse(side,DOOR_BACK,t), 0,1,13,.036)
        body.grid("Glass", steps(-1.69,DOOR_BACK-.027,24), steps(.09,.93,8),
                  lambda z,t: greenhouse(side,z,t), (side,0,0))
        for row in (-.752,.10):
            back_y = .856 if row>0 else .814
            body.rounded_box("Cabin", (side*.413,.575,row), (.464,.156,.478), .071)
            body.rounded_box("Cabin", (side*.413,back_y,row-.211), (.442,.476,.139), .062)
            body.rounded_box("Cabin", (side*.413,1.10 if row>0 else 1.03,row-.219), (.226,.131,.138), .047)
            for offset in (-.184,.184):
                x = side*.413+offset
                body.rounded_box("Graphite",(x,.617,row+.004),(.075,.103,.398),.036)
                body.rounded_box("Graphite",(x,back_y,row-.156),(.073,.399,.099),.029)
                trace(body,"Satin",lambda z:(x,.656+.004*sin((z-row)*12),z),row-.13,row+.144,16,.0016)
    dashboard_cabin(body)

    def fascia(u,t):
        x,y,z = nose_station(u)
        return x * (.95+.05*sin(pi*t*.88)), lerp(.322+.04*abs(u)**2, y,t), z-.081*(1-t)**2+.014*sin(pi*t)

    body.grid("Paint", steps(-1,1,46), steps(0,1,16), fascia, (0,0,1))
    for side in (-1,1):
        def bumper_return(t,k):
            z = 2.11
            side_edge = flank(side,z,lerp(lower(z),belt(z),t))
            fascia_edge = fascia(side,t)
            x,y,z = (lerp(a,b,k) for a,b in zip(side_edge,fascia_edge))
            return x+side*.006*sin(pi*k),y,z

        body.grid("Paint",steps(0,1,16),steps(0,1,6),bumper_return,(side,0,1))
    body.grid("Graphite", steps(-.79,.79,38), steps(.083,.30,7),
              lambda u,t: tuple(fascia(u,t)[i]+(.010 if i==2 else 0) for i in range(3)), (0,0,1))
    for t in (.125,.19,.255):
        trace(body, "Seal", lambda u: (fascia(u,t)[0],fascia(u,t)[1]-.005,fascia(u,t)[2]+.014), -.74,.74,32,.004)
    for side in (-1, 1):
        def lamp(u,t, depth):
            s=(u-.37)/.603
            spread=sin(pi*s)**.59
            x,y,z=fascia(side*u,.865+.061*s+(t-.5)*.168*spread)
            return x,y,z+depth

        body.grid("Lens",steps(.371,.972,40),steps(0,1,8),
                  lambda u,t:lamp(u,t,.012),(0,0,1))
        trace(body,"Ice",lambda u:lamp(u,.792,.018),.385,.966,38,.0055)
        trace(body,"Satin",lambda u:lamp(u,.241,.017),.412,.951,35,.0028)
        for station in (.647,.755,.858):
            x,y,z=lamp(station,.426,.016)
            body.rounded_box("Seal",(x,y,z),(.048,.028,.016),.009,yaw=side*.23)
            body.rounded_box("Ice",(x,y,z+.011),(.031,.011,.008),.0035,yaw=side*.23)
    for side in (-1,1):
        for station in (.26,.57):
            x,y,z=fascia(side*station,.46)
            body.rounded_box("Seal",(x,y,z+.005),(.023,.023,.006),.0025)

    def rear(u,t):
        x,y,z = hatch_surface(u,1)
        return x*(.9+.1*t), lerp(.34,y,t),z+.085*(1-t)**2-.026*sin(pi*t)

    body.grid("Paint", steps(-1,1,42), steps(0,1,14), rear, (0,0,-1))
    body.grid("Graphite", steps(-.91,.91,38),steps(0,.36,6), lambda u,t: (rear(u,t)[0],rear(u,t)[1],rear(u,t)[2]-.014),(0,0,-1))
    for side in (-1,1):
        def rear_corner(t,k):
            z = REAR_SHOULDER
            start = flank(side,z,lerp(lower(z),belt(z),t))
            end = rear(side,t)
            x,y,z = (lerp(a,b,k) for a,b in zip(start,end))
            return x+side*.004*sin(pi*k),y,z

        body.grid("Paint",steps(0,1,20),steps(0,1,10),rear_corner,(side,0,-1))

        def tail(u,t):
            height=.84+.058*(abs(u)-.4)
            x,y,z=rear(side*u,height+(t-.5)*.115*sin(pi*(u-.41)/.555)**.45)
            return x,y,z-.014

        body.grid("Lens",steps(.412,.963,36),steps(0,1,7),tail,(0,0,-1))
        for t,a in ((.74,.424),(.35,.573)):
            trace(body,"Tail",lambda u,t=t:tuple(v-(.009 if i==2 else 0) for i,v in enumerate(tail(u,t))),a,.955,33,.006)
        x,y,z=rear(side*.788,.185)
        body.rounded_box("Tail",(x,y,z-.014),(.093,.012,.013),.005,yaw=-side*.12)
    body.rounded_box("Graphite", (0,.416,-.18), (1.62,.105,2.7), .047)
    body.rounded_box("Graphite", (0,.831,.577), (1.56,.18,.304), .085)
    body.rounded_box("Seal", (0,.626,.04), (.165,.20,.88), .059)
    trace(body,"Seal",lambda a:(-.385+.143*cos(a),.867+.129*sin(a),.405-.054*sin(a)),0,2*pi,32,.017)
    body.finish()

    hood = Component("Hood", root, (0,1.02,.857))
    hood_width = steps(-.982,.982,28)
    hood_length = steps(.006,.995,35)

    def hood_inner(u,t):
        x,y,z = hood_surface(u,t)
        return x,y-.017-.009*sin(pi*t)*(1-u*u),z

    hood.grid("Paint",hood_width,hood_length,hood_surface,(0,1,0))
    hood.grid("Graphite",hood_width,hood_length,hood_inner,(0,-1,0))
    for u in (-.982,.982):
        hood.grid("Paint",hood_length,(0,1), lambda t,k: tuple(lerp(a,b,k) for a,b in
            zip(hood_surface(u,t),hood_inner(u,t))),(u,0,0))
    for t in (.006,.995):
        hood.grid("Paint",hood_width,(0,1), lambda u,k: tuple(lerp(a,b,k) for a,b in
            zip(hood_surface(u,t),hood_inner(u,t))),(0,0,1 if t > .5 else -1))
    for side in (-1,1):
        trace(hood,"Satin",lambda t: tuple(a- (.006 if i == 1 else 0) for i,a in
            enumerate(hood_inner(side*lerp(.26,.63,t),t))),.12,.89,25,.012)
    hood.finish()

    hatch = Component("Trunk", root, (0,1.51,-.925))
    hatch.grid("Glass",steps(-.985,.985,23),steps(.015,.67,21),hatch_surface,(0,1,-1))
    hatch.grid("Paint",steps(-.993,.993,25),steps(.675,.997,13),hatch_surface,(0,1,-1))
    trace(hatch,"Seal",lambda u: hatch_surface(u,.67),-.995,.995,29,.008)
    def spoiler(u,depth):
        x,y,z=hatch_surface(u,.105)
        taper=(1-u*u)**.6
        return x,y+.002+.021*depth*taper,z-.071*depth*taper
    hatch.grid("Paint",steps(-1,1,40),steps(0,1,7),spoiler,(0,1,0))
    trace(hatch,"Seal",lambda u:spoiler(u,1),-.998,.998,40,.003)
    hatch.finish()


def door(root, side, front, suffix):
    a, b = (DOOR_SPLIT+.004, DOOR_FRONT-.006) if front else (DOOR_BACK+.006, DOOR_SPLIT-.005)
    part = Component("Door_"+suffix,root,(side*(width(b)-.024),.84,b-.012))

    def door_flank(z,t):
        return flank(side,z,lerp(.399,belt(z)-.005,t))

    part.grid("Paint",steps(a,b,34),steps(0,1,14),door_flank,(side,0,0))
    for z in (a,b):
        trace(part,"Seal",lambda t: door_flank(z,t),0,1,15,.004)
    def belt_cap(z,f):
        return tuple(lerp(a1,b1,f) for a1,b1 in zip(door_flank(z,1),greenhouse(side,z,.006)))

    part.grid("Seal",steps(a+.008,b-.007,35),steps(0,1,6),belt_cap,(0,1,0))
    trace(part,"Seal",lambda z:door_flank(z,.997),a,b,35,.004)
    part.grid("Glass",steps(a+.013,b-.009,30),steps(.007,.974,12),lambda z,t: greenhouse(side,z,t),(side,0,0))
    for z in (a+.009,b-.008):
        trace(part,"Seal",lambda t: greenhouse(side,z,t),.03,.98,14,.010)
    trace(part,"Seal",lambda z: greenhouse(side,z,.973),a+.012,b-.013,30,.009)
    x,y,z = door_flank(a+.156,.78)
    part.rounded_box("Seal",(x+side*.004,y,z),(.011,.032,.151),.005,yaw=side*.06)
    part.rounded_box("Satin",(x+side*.009,y+.001,z),(.009,.017,.126),.004,yaw=side*.06)

    def trim(z,t):
        x,y,z = door_flank(z,t)
        offset = .032+.023*sin(pi*t)
        return x-side*offset,y,z

    part.grid("Cabin",steps(a+.016,b-.019,26),steps(.085,.962,12),trim,(-side,0,0))
    for z in (a,b):
        part.grid("Seal",steps(.085,.962,12),(0,1),lambda t,k:tuple(lerp(m,n,k) for m,n in
            zip(door_flank(z,t),trim(z,t))), (0,0,1 if z==b else -1))
    trace(part,"Seal",lambda z:trim(z,.939),a+.014,b-.014,25,.011)
    trace(part,"Satin",lambda z:trim(z,.797),a+.10,b-.08,26,.005)
    trace(part,"Graphite",lambda z:trim(z,.56),a+.12,b-.14,26,.026)
    ix,iy,iz=trim(b-.186,.705)
    part.rounded_box("Satin",(ix-side*.011,iy,iz),(.013,.024,.104),.006)
    ix,iy,iz=trim(b-.226,.29)
    part.rounded_box("Liner",(ix-side*.012,iy,iz),(.022,.113,.175),.01)
    for notch in steps(-.063,.063,7):
        part.tube("Graphite",[(ix-side*.025,iy-.041,iz+notch),(ix-side*.026,iy+.039,iz+notch)],.0027,6)
    if front:
        x,y,z = greenhouse(side,b-.102,.18)
        part.tube("Graphite",[(x,y-.037,z),(x+side*.097,y+.024,z-.041)],.024,9)
        part.rounded_box("Paint",(x+side*.134,y+.041,z-.067),(.206,.092,.147),.044,side*.20)
        part.rounded_box("Seal",(x+side*.137,y+.040,z-.136),(.167,.064,.015),.007,side*.20)
        part.rounded_box("Lens",(x+side*.138,y+.041,z-.143),(.146,.049,.0047),.0018,side*.20)
        part.rounded_box("Ice",(x+side*.139,y+.026,z+.003),(.081,.0048,.006),.0021,side*.20)
    part.finish()


def wheel(root, side, front, suffix):
    center = (side*WHEEL_X,WHEEL_Y,AXLE if front else -AXLE)
    component = Component("Wheel_"+suffix,root,center)

    def face(material, profile):
        mirrored = [(x*side,radius) for x,radius in profile]
        component.lathe(material,center,mirrored if side == 1 else mirrored[::-1])

    component.lathe("Tire",center,[(-.105,.251),(-.121,.284),(-.129,.322),(-.119,.349),(-.102,.365),
                                      (-.086,.372),(-.066,.373),(.066,.373),(.086,.372),(.102,.365),
                                      (.119,.349),(.129,.322),(.121,.284),(.105,.251),(-.105,.251)],96)
    for x in (-.064,0,.064):
        component.lathe("Tread",center,[(x-.006,.3735),(x+.006,.3735)],96)
    for line in (0,1):
        for i in range(45):
            theta = (i + .36*line) * 2*pi/45
            component.grid("Tread",(-1,1),(-1,1),lambda u,v: (
                center[0]+(.049 if line else -.049)+.032*u, center[1]+.3738*cos(theta+.026*u+.005*v),
                center[2]+.3738*sin(theta+.026*u+.005*v)),lambda p: (0,p.y-center[1],p.z-center[2]))

    face("Graphite",[(.037,.0),(.052,.232),(.081,.255),(.126,.267),(.139,.251),(.127,.239),(.105,.226),(.091,0)])
    face("Alloy",[(.125,.239),(.139,.246),(.143,.255),(.137,.264),(.124,.267)])
    face("Satin",[(.072,.062),(.076,.16),(.086,.177),(.088,.212),(.084,.221),(.068,.221),(.063,.214),(.064,.063)])
    face("Graphite",[(.080,.059),(.091,.064),(.092,.12),(.089,.157),(.081,.162)])
    for index in range(16):
        angle=index*2*pi/16
        component.tube("Liner",[(center[0]+side*.090,center[1]+r*cos(angle+curve),
            center[2]+r*sin(angle+curve)) for r,curve in ((.178,0),(.191,.015),(.208,.037))],.0023,6)
    face("Tire",[(.120,.289),(.124,.294),(.124,.298),(.122,.300)])
    face("Tire",[(.128,.329),(.1286,.333),(.127,.336)])
    face("Graphite",[(.144,.081),(.151,.064),(.148,.012),(.141,0)])
    face("Alloy",[(.153,.041),(.165,.031),(.165,0)])

    # Turbine spokes are thin sculpted surfaces with bevelled front faces and depth.
    for index in range(10):
        phase = index*2*pi/10

        def spoke(r, edge, outside):
            angle = phase + side*(.18*sin(r*1.66) + edge*(.114-.043*r))
            radius = lerp(.065,.252,r)
            x = .106 + .027*sin(r*pi*.89) if outside else .087 + .022*sin(r*pi*.89)
            x += .006*sin(pi*(edge+1)*.5) if outside else 0
            return center[0]+side*x,center[1]+radius*cos(angle),center[2]+radius*sin(angle)

        component.grid("Alloy",steps(0,1,8),steps(-1,1,5),lambda r,e: spoke(r,e,True),(side,0,0))
        component.grid("Graphite",steps(0,1,8),steps(-1,1,5),lambda r,e:spoke(r,e,False),(-side,0,0))
        for edge in (-1,1):
            component.grid("Satin",steps(0,1,8),(0,1),lambda r,t: tuple(lerp(a,b,t) for a,b in
                zip(spoke(r,edge,False),spoke(r,edge,True))),lambda p:(0,-edge*(p.z-center[2]),edge*(p.y-center[1])))
        if index % 2 == 0:
            angle = phase + .24
            hub_center = (center[0]+side*.149,center[1]+.050*cos(angle),center[2]+.050*sin(angle))
            component.lathe("Alloy",hub_center,[(-.004,0),(-.004,.009),(.004,.009),(.004,0)],12)

    component.finish()


def battery(root):
    pack = Component("Battery",root,(0,.31,-.11))
    pack.rounded_box("Graphite",(0,.252,-.145),(1.563,.202,2.679),.074)
    pack.rounded_box("Satin",(0,.348,-.145),(1.513,.064,2.553),.031)
    pack.rounded_box("Graphite",(0,.393,-.145),(1.452,.026,2.478),.011)
    for side in (-1,1):
        pack.rounded_box("Satin",(side*.735,.329,-.145),(.062,.084,2.547),.024)
        trace(pack,"Coolant",lambda z:(side*.696,.433,z),-1.344,1.021,21,.014)
        for z in steps(-1.26,.97,9):
            pack.rounded_box("Alloy",(side*.735,.385,z),(.059,.033,.076),.014)
    for row in range(5):
        z = -1.117+row*.48
        for side in (-1,1):
            x = side*.339
            pack.rounded_box("Battery",(x,.420,z),(.643,.058,.446),.026)
            for seam in (-.106,.038,.181):
                trace(pack,"Graphite",lambda at:(x+seam,.452,z+at),-.18,.185,2,.004)
            for link in (-.098,.106):
                pack.rounded_box("Coolant",(x,.453,z+link),(.595,.012,.028),.005)
    pack.rounded_box("Graphite",(0,.453,-.15),(.056,.048,2.38),.018)
    pack.rounded_box("HV",(.37,.354,1.184),(.184,.117,.154),.029)
    pack.rounded_box("Graphite",(-.30,.348,1.152),(.32,.103,.186),.027)
    pack.finish()


def e_drive(root):
    motor = Component("Motor_F",root,(0,.477,AXLE))
    motor.lathe("Satin",(-.174,.493,AXLE),[(-.245,0),(-.243,.187),(-.217,.232),(-.169,.248),
                 (.138,.248),(.198,.223),(.224,.187),(.225,0)],64)
    for i in range(14):
        x = -.365+i*.024
        motor.lathe("Alloy",(x,.493,AXLE),[(-.007,.217),(-.006,.245),(.005,.245),(.008,.217)],48)
    motor.lathe("Graphite",(.286,.398,AXLE-.012),[(-.178,0),(-.171,.172),(-.116,.209),(.082,.209),(.132,.152),(.132,0)],56)
    motor.lathe("Alloy",(.412,.395,AXLE-.044),[(-.039,.143),(.016,.146),(.035,.097),(.036,0)],42)
    motor.rounded_box("Satin",(-.09,.751,AXLE-.094),(.637,.171,.471),.069)
    motor.rounded_box("Graphite",(-.09,.838,AXLE-.094),(.549,.032,.392),.014)
    for index in range(12):
        x = -.325 + index*.044
        motor.rounded_box("Alloy",(x,.86,AXLE-.095),(.016,.028,.369),.007)
    for side in (-1,1):
        # Half shafts and exposed battery connectors are readable in X-Ray mode.
        motor.tube("Satin",[(side*.315,.407,AXLE-.018),(side*.527,.379,AXLE-.017),(side*.818,WHEEL_Y,AXLE)],.041,12)
        motor.lathe("Graphite",(side*.617,.378,AXLE),[(-.066,.043),(-.051,.062),(-.03,.058),(-.013,.067),(.005,.063),(.032,.044)],38)
        motor.rounded_box("HV",(side*.286,.745,AXLE-.328),(.099,.085,.096),.020)
        motor.tube("HV",[(side*.286,.745,AXLE-.365),(side*.303,.702,AXLE-.429),(side*.344,.565,AXLE-.483),
                        (side*.397,.449,AXLE-.542),(side*.406,.357,AXLE-.621)],.022,12)
    motor.finish()


def main():
    tail = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    repository = Path(__file__).resolve().parents[2]
    export = Path(tail[0]).resolve() if tail else repository / "Assets/AutoTwin/Art/Vehicle/EV01 Concept.fbx"
    if not export.parent.is_dir():
        raise NotADirectoryError(export.parent)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    for label, color in COLORS.items():
        material = bpy.data.materials.new(label)
        material.diffuse_color = (*color,1)
        material.use_nodes = True
        shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
        shader.inputs["Base Color"].default_value = (*color,1)
        shader.inputs["Metallic"].default_value = .58 if label=="Paint" else .74 if label in ("Alloy","Satin","Copper") else .0
        shader.inputs["Roughness"].default_value = {
            "Glass": .235, "Lens": .178, "Paint": .326, "Satin": .38, "Alloy": .31, "Tire": .72,
        }.get(label,.52)
        if label=="Paint":
            shader.inputs["Coat Weight"].default_value=.42
            shader.inputs["Coat Roughness"].default_value=.28
        if label in ("Ice","Tail","Display"):
            shader.inputs["Emission Color"].default_value=(*color,1)
            shader.inputs["Emission Strength"].default_value=.52 if label=="Ice" else .26

    root = bpy.data.objects.new("VehicleRoot",None)
    bpy.context.collection.objects.link(root)
    bodywork(root)
    for front in (True,False):
        for side in (-1,1):
            suffix = ("F" if front else "R") + ("L" if side == -1 else "R")
            door(root,side,front,suffix)
            wheel(root,side,front,suffix)
    battery(root)
    e_drive(root)

    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,object_types={"EMPTY","MESH"},
                            global_scale=1,apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",
                            axis_forward="-Z",axis_up="Y",use_mesh_modifiers=True,bake_anim=False,
                            bake_space_transform=False,add_leaf_bones=False,mesh_smooth_type="FACE")
    meshes = [item.data for item in bpy.data.objects if item.type == "MESH"]
    print(f"EV01: {len(root.children)} assemblies, {len(meshes)} meshes, "
          f"{sum(len(mesh.polygons) for mesh in meshes):,} faces -> {export}")
    if len(tail)>1:
        bpy.context.scene.name="AutoTwin EV-01 Precision"
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(Path(tail[1]).resolve()),compress=True)


if __name__ == "__main__":
    main()
