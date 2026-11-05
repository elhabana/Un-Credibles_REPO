"""Builds previs_edl.json with every gameplay cut on the music's beat grid.

Each shot is listed with its length in beats (or seconds for the intro/outro) and the time inside the
take where it starts. Sound effects are placed relative to their shot, at the second of the game event
read from the takes' _events.txt. Run it, then assemble_previs.py.
"""
import json
import os

FPS = 24
BEAT = 60 / 107.67          # tempo of the music
DROP_IN_FILE = 8.14         # the drums kick in here in the music file
GAMEPLAY = 10.0             # ...and here in the trailer (cut from the title to the road)

HERE = os.path.dirname(os.path.abspath(__file__))
ANIMATIC = ("C:/Users/haban/Downloads/ANIMATICA_MarioMarquina_CristianRivero(Habana)/"
            "ANIMATICA_MarioMarquina_CristianRivero(Habana)/UN_CREDIBLES_Trailer_Animatica.mp4")


def snap(t):
    return round(t * FPS) / FPS


# (shot, label, take or split, in, beats | ("s", seconds), [(sfx file, offset in shot, gain, extra)])
CR, CH, MW, GM = "P04-06_CrossyRoad_t6_", "P07-09_Churro_t4_", "P10-12_Mow_t4_", "P13-15_Mice_t4_"
# t8: bigger oil truck, the arrival camera moved out of the tree row, close follow cameras for the slips.
CR8 = "P04-06_CrossyRoad_t8_"
# t5: wide follow cameras for the crashes and fixed cameras by each bin.
MW5 = "P10-12_Mow_t5_"
SHOTS = [
    ("01", "Garaje - la persiana se abre y salen corriendo hacia la luz", "P01_Garaje_t2.mp4", 0.0, ("s", 6.5), [
        ("sfx_garage_door_opening.mp3", 0.4, -2, {"dur": 3.2, "fade": 0.6}),
        # The take has ~1.16 s of pre-roll: the run starts at frame 97.5 (4.06 s), not at runStart (2.9 s).
        # Run clip 0.6 s at 1.15x = a footfall every 0.261 s, the first one 0.25 s in (4.31, 4.57, 4.83, 5.09);
        # the effect has one every 0.29 s from 0.12 s, so sped up 1.12x it lands on them, fading into the light.
        ("sfx_running_floor.mp3", 4.2, -3, {"dur": 1.25, "fade": 0.4, "tempo": 1.12}),
        ("sfx_whoosh.mp3", 5.6, -3, {})]),
    ("03", "Rotulo - presentacion del reto", "P03-18_Titulos_t2.mp4", 0.0, ("s", 3.5), [
        ("sfx_whoosh.mp3", 3.2, -5, {})]),
    # Crossing the road: crane over the traffic, the slip, the grannies reaching the kerb.
    ("04", "Paso de peatones - cruce con las abuelas (grua)", CR8 + "Grua.mp4", 8.0, 8, [
        ("sfx_cinematic_impact.mp3", 0.0, -9, {}),
        ("sfx_traffic_city.mp3", 0.0, -11, {"dur": 11.1, "fade": 0.5}),
        ("sfx_car_horn_double.mp3", 1.3, -6, {}),
        ("sfx_car_horn_double.mp3", 3.4, -11, {})]),
    # Red alone on the slick: IsSlipping 8.50-9.42, then flailing on his own; Blue walks in after 10 s,
    # so the shot is three beats and the arrival gets the other three.
    ("05", "Paso de peatones - resbalon en aceite", CR8 + "Resbalon_Rojo.mp4", 8.3, 3, [
        ("sfx_cartoon_slide.mp3", 0.2, -2, {"dur": 1.2, "fade": 0.3})]),
    # The big oil truck passes the camera, then Blue (12.33) and Red (13.08) drop their grannies on the kerb.
    ("06", "Paso de peatones - las abuelas llegan a la acera", CR8 + "Llegada_dolly.mp4", 11.0, 9, [
        ("sfx_car_horn_double.mp3", 0.35, -8, {}),
        ("sfx_tada_fanfare.mp3", 1.33, -3, {}),
        ("sfx_points_positive.mp3", 2.08, -7, {})]),
    # Pool.
    ("07", "Piscina - presentacion del churro giratorio", CH + "Alta_tres_cuartos.mp4", 0.5, 7, [
        ("sfx_whoosh.mp3", -0.25, -6, {}),
        ("sfx_pool_water.mp3", 0.0, -9, {"dur": 11.1, "fade": 0.5})]),
    ("08", "Piscina - salto para evitar el churro", CH + "Fija_Rojo.mp4", 4.3, 7, [
        ("sfx_cartoon_boing.mp3", 1.2, -3, {}),
        ("sfx_water_splash.mp3", 3.2, -12, {"dur": 0.8, "fade": 0.3})]),
    ("09a", "Piscina - el nino se lanza en bomba", CH + "Alta_tres_cuartos.mp4", 54.75, 4, [
        ("sfx_cartoon_slide.mp3", 0.9, -12, {"dur": 1.2, "fade": 0.3})]),
    ("09b", "Piscina - la salpicadura cubre la camara", CH + "Juego.mp4", 56.98, 2, [
        ("sfx_big_splash.mp3", -0.05, 1, {})]),
    # Garden.
    ("10", "Jardin - cortacespedes y filas de bolsas (grua)", MW + "Grua.mp4", 29.3, 7, [
        ("sfx_whoosh.mp3", -0.25, -6, {}),
        ("sfx_lawn_mower.mp3", 0.0, -10, {"dur": 11.1, "fade": 0.6})]),
    # Yellow drags four bags; Green rams him at 37.17 (IsStunned, BagCount 4 -> 0) and they scatter on the lawn.
    ("11", "Jardin - choque y perdida de carga", MW5 + "Choque_Amarillo.mp4", 35.6, 7, [
        ("sfx_cartoon_impact.mp3", 1.57, -1, {}),
        ("sfx_bag_drop.mp3", 1.7, -3, {})]),
    # Red pulls up beside his bin and the bags fly into it one by one (BagCount 5 -> 0, 30.50-31.00).
    ("12", "Jardin - entrega de bolsas", MW5 + "Contenedor_Rojo.mp4", 28.9, 6, [
        ("sfx_bag_drop.mp3", 1.6, -7, {}),
        ("sfx_points_positive.mp3", 2.1, -4, {})]),
    # Garage mice: the green hero sweeps the red one, who stays stunned.
    ("13", "Almacen - los heroes persiguen ratones", GM + "Sala_tres_cuartos.mp4", 8.0, 7, [
        ("sfx_whoosh.mp3", -0.25, -6, {}),
        ("sfx_mouse_squeaks.mp3", 0.0, -7, {"dur": 6.7, "fade": 0.5}),
        ("sfx_running_floor.mp3", 0.2, -10, {"dur": 3.5, "fade": 0.4})]),
    ("14", "Almacen - el verde le da un escobazo al rojo", GM + "Cerca_Verde.mp4", 33.25, 6, [
        ("sfx_cartoon_bonk.mp3", 2.13, 2, {"cut_music": True})]),
    ("15", "Almacen - el rojo se queda aturdido", GM + "Cerca_Rojo.mp4", 35.45, 7, [
        ("sfx_dizzy_whistle.mp3", 0.0, -3, {"dur": 3.8, "fade": 0.6})]),
    # Montage: keep two-beat cuts through the fast sequence, then show all four games at once.
    ("16a", "Montaje - resbalon con abuela", CR8 + "Lateral_Azul.mp4", 19.7, 2, [
        ("sfx_cinematic_impact.mp3", 0.0, -6, {}), ("sfx_cartoon_slide.mp3", 0.18, -4, {"dur": 0.8, "fade": 0.2})]),
    ("16b", "Montaje - salto en la piscina", CH + "Fija_Rojo.mp4", 8.2, 2, [
        ("sfx_cartoon_boing.mp3", 0.5, -4, {})]),
    ("16c", "Montaje - cortacesped con seis bolsas", MW + "Lateral_Rojo.mp4", 32.0, 2, [
        ("sfx_cartoon_impact.mp3", 1.0, -4, {})]),
    ("16d", "Montaje - escobazo al azul", GM + "Cerca_Azul.mp4", 22.6, 2, [
        ("sfx_cartoon_bonk.mp3", 0.53, -2, {})]),
    ("16e", "Montaje - trafico desde la grua", CR8 + "Grua.mp4", 33.6, 2, [("sfx_car_horn_double.mp3", 0.0, -10, {})]),
    ("16f", "Montaje - eliminado en la piscina", CH + "Agua_dolly.mp4", 24.3, 2, [("sfx_water_splash.mp3", 0.2, -5, {"dur": 0.5, "fade": 0.2})]),
    ("16g", "Montaje - siete bolsas", MW + "Grua.mp4", 52.9, 2, []),
    ("16h", "Montaje - aturdido en el almacen", GM + "Sala_tres_cuartos.mp4", 44.2, 2, [("sfx_cartoon_bonk.mp3", 0.35, -6, {})]),
    ("16k", "Montaje - pantalla partida: los cuatro minijuegos", [
        (CR8 + "Grua.mp4", 39.6), (CH + "Alta_tres_cuartos.mp4", 20.0),
        (MW + "Grua.mp4", 40.0), (GM + "Sala_tres_cuartos.mp4", 25.0)], None, 4, [
        ("sfx_car_horn_double.mp3", 0.2, -11, {}),
        ("sfx_crowd_cheer.mp3", 0.5, -12, {"dur": 1.8, "fade": 0.6})]),
    ("17", "Podio - clasificacion final", "P17_Podio_t4.mp4", 0.0, ("until", 70.0), [
        ("sfx_points_positive.mp3", 0.0, -3, {}),
        ("sfx_tada_fanfare.mp3", 0.5, -6, {}),
        ("sfx_crowd_cheer.mp3", 0.6, -7, {"dur": 3.4, "fade": 0.8}),
        ("sfx_whoosh.mp3", 3.95, -5, {})]),
    ("18", "Logotipo - UN-CREDIBLES y eslogan", "P03-18_Titulos_t2.mp4", 3.5, ("until", 75.0), [
        ("sfx_cinematic_impact.mp3", 0.1, 0, {}),
        ("sfx_crowd_cheer.mp3", 0.3, -14, {"dur": 2.6, "fade": 1.2})]),
]


def main():
    t = 0.0
    shots, sfx = [], []
    music_cut = None
    for shot, label, take, start, length, effects in SHOTS:
        if isinstance(length, tuple):
            kind, value = length
            end = snap(t + value) if kind == "s" else snap(value)
        else:
            beat_index = round((t - GAMEPLAY) / BEAT)
            end = snap(GAMEPLAY + (beat_index + length) * BEAT)
        dur = round(end - t, 6)
        entry = {"shot": shot, "label": label, "dur": dur, "at": round(t, 3)}
        if isinstance(take, list):
            entry["split"] = [{"take": name, "in": s} for name, s in take]
        else:
            entry["take"], entry["in"] = take, start
        shots.append(entry)
        for name, offset, gain, extra in effects:
            item = {"file": name, "at": round(t + offset, 3), "gain_db": gain, "note": shot}
            for key in ("dur", "fade", "tempo", "in"):
                if key in extra:
                    item[key] = extra[key]
            if extra.get("cut_music"):
                music_cut = t + offset
            sfx.append(item)
        t = end
    music_at = GAMEPLAY - DROP_IN_FILE
    podium_at = next(s["at"] for s in shots if s["shot"] == "17")
    logo_at = next(s["at"] for s in shots if s["shot"] == "18")
    duck = [[0.0, 6.6, -6], [GAMEPLAY - 3.5, GAMEPLAY - 0.05, -2], [podium_at, logo_at, -9]]
    if music_cut is not None:
        duck.append([round(music_cut - 0.35, 3), round(music_cut + 0.9, 3), -16])
    edl = {
        "title": "UN-CREDIBLES - Previs 3D del trailer",
        "fps": FPS, "width": 1920, "height": 1080,
        "guide_audio": ANIMATIC,
        "notes": "Generated by build_edl.py: edit SHOTS there, not this file. Every gameplay cut lands on the "
                 "music's beat (107.7 BPM); the drums kick in at 10 s on the cut to the road.",
        "audio": {
            "music": {"file": "music_B_UpliftingActionSportRock_ikoliks.mp3", "at": round(music_at, 3), "in": 0.0,
                      "gain_db": -4, "duck": duck, "fade_out": [round(logo_at + 2.0, 3), 2.0]},
            "sfx": sfx,
        },
        "shots": shots,
    }
    with open(os.path.join(HERE, "previs_edl.json"), "w", encoding="utf8") as f:
        json.dump(edl, f, ensure_ascii=False, indent=1)
    print(f"{len(shots)} shots, {t:.3f} s")
    for s in shots:
        print(f"  {s['at']:7.3f}  {s['dur']:6.3f}  {s['shot']:4} {s['label']}")


if __name__ == "__main__":
    main()
