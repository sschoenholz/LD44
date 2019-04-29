using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum VisualizationMode
{
    Blood, Pressure, Oxygen,
}

public enum TileConnection
{
    Up, Down, Left, Right
}

public enum TileType
{
    Empty,
    Flesh,
    Vein,
    Lung,
    Heart,
    Alive,
}

public class Tile : MonoBehaviour
{
    public VisualizationMode vis_mode;

    public TileType type;
    public float blood;
    public float pressure;
    public float oxygen;

    public float blood_max;
    public float oxygen_max;

    public float blood_reserve;
    public float oxygen_reserve;

    public Stack<Tile> connection;
    public Tile[] debug_connection;

    public int x, y;
    public bool has_connections_established;

    public bool is_locked;
    public bool is_building;
    public bool is_dead;
    public bool is_dead_next;

    public GameObject child_tile;
    public GameObject child_tile_2;

    public Map map;

    private AudioSource audio_source;

    Tile()
    {
        connection = new Stack<Tile>();
        blood = 0.0f;
        pressure = 0.0f;
        oxygen = 0.0f;
        vis_mode = VisualizationMode.Blood;
        is_dead = false;
        is_dead_next = false;
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    private float last_breath;
    private float last_beat;

    // Update is called once per frame
    void Update()
    {
        if (is_dead_next && !is_dead)
            is_dead = true;

        if (map.state == State.LoseMenu)
            return;

        var t = Time.time;
        var T = GetComponent<Transform>();

        var ones = new Vector3(1.0f, 1.0f, 1.0f);

        if (type == TileType.Vein)
        {
            if (!is_building)
            {
                if (vis_mode == VisualizationMode.Blood)
                    T.localScale = (blood + 0.01f) * ones;
                else if (vis_mode == VisualizationMode.Pressure)
                    T.localScale = (pressure + 0.01f) * ones;
                else if (vis_mode == VisualizationMode.Oxygen)
                    T.localScale = (oxygen + 0.01f) * ones;

                var child_T = child_tile.GetComponent<Transform>();
                float inv = (1.0f / T.localScale.x);
                child_T.localScale = new Vector3(inv, inv, 1.0f);

                TransferBlood(blood, oxygen);
            } else
            {
                if (blood > map.blood_prices[(int)TileType.Vein])
                {
                    is_building = false;
                    blood = 0.0f;
                    audio_source.pitch = map.vein_sound_pitch;
                    audio_source.PlayOneShot(map.build_vein_sound);
                    map.vein_last_build = Time.time;
                    map.vein_sound_pitch += 0.025f;
                }
            }
        }

        if (type == TileType.Heart)
        {
            var pump = Mathf.Sin(4.0f * t);

            var tmod = (4.0f * t) % (2.0f * Mathf.PI);
            var isigma = Mathf.Pow(1.0f * Mathf.PI / 4.0f, -2.0f);
            var mean = Mathf.Pow(tmod - 2.0f * Mathf.PI / 2.0f, 2.0f);
            var blood_transfer = map.heart_pump_blood * Mathf.Exp(-isigma * mean);
            T.localScale = 0.5f * ones * (pump + 1.0f) + 1.35f * ones;
            TransferBlood(blood_transfer, 0.0f);

            if (Mathf.Abs(tmod - Mathf.PI - 0.0f) < 0.1f && t - last_beat > 1.0f)
            {
                last_beat = t;
                audio_source.pitch = 0.6f + 0.1f * (Random.value - 0.5f);
                audio_source.PlayOneShot(map.heart_sound);
            }
        }

        if (type == TileType.Lung)
        {
            T.localEulerAngles = -new Vector3(0.0f, 0.0f, 1.0f) * t * 100.0f;
            float oxygen_transfer = blood;
            TransferBlood(blood, oxygen_transfer);

            var tmod = t % 5f;

            if (tmod < 0.1f && t - last_breath > 1.0f)
            {
                last_breath = t;
                audio_source.pitch = 1.0f + 0.2f * (Random.value - 0.5f);
                audio_source.PlayOneShot(map.breath_sound);
            }
        }

        if (type == TileType.Alive && map.state == State.Playing)
        {
            blood_reserve -= map.alive_blood_rate;
            oxygen_reserve -= map.alive_oxygen_rate;

            var dblood = Mathf.Min(blood_max - blood_reserve, blood);
            blood_reserve += dblood;
            blood -= dblood;

            var doxygen = Mathf.Min(oxygen_max - oxygen_reserve, oxygen);
            oxygen_reserve += doxygen;
            oxygen -= doxygen;

            TransferBlood(blood, oxygen);

            if (oxygen_reserve < 0 || blood_reserve < 0)
                map.LoseGame(this);

            T.localScale = ones * 1.5f * blood_reserve / blood_max;
            var child_T = child_tile.GetComponent<Transform>();
            child_T.localScale = ones * 1.5f * oxygen_reserve / oxygen_max;
        }
    }

    public void SetType(TileType _type)
    {
        Renderer renderable = GetComponent<Renderer>();
        type = _type;

        blood = 0.0f;
        pressure = 0.0f;
        oxygen = 0.0f;
        is_dead = false;
        is_dead_next = false;
        is_building = false;
        is_locked = false;
        audio_source = null;

        if (child_tile != null)
        {
            Destroy(child_tile);
            child_tile = null;
        }
        if (child_tile_2 != null)
        {
            Destroy(child_tile_2);
            child_tile_2 = null;
        }

        Transform T = GetComponent<Transform>();

        if (type == TileType.Empty)
            renderable.enabled = false;

        if (type == TileType.Vein)
        {
            renderable.enabled = true;
            child_tile = Instantiate(map.child_tile);
            Transform child_T = child_tile.GetComponent<Transform>();
            var child_R = child_tile.GetComponent<Renderer>();
            child_R.material.SetColor("_Color", new Color(0.8f, 0f, 0f));
            child_T.parent = T;
            child_T.localPosition = new Vector3(0f, 0f, 1f);
            SetTileColor();
            audio_source = map.vein_audio_source;
        }
        if (type == TileType.Heart)
        {
            renderable.enabled = true;
            renderable.material.SetColor(
                "_Color", new Color(1.0f, 0.0f, 0.0f));
            pressure = map.heart_pump_pressure;
            audio_source = map.heart_audio_source;
        }
        if (type == TileType.Flesh)
        {
            T.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            T.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
            renderable.enabled = true;
            renderable.material.SetColor(
                "_Color", new Color(1.0f, 173f / 255f, 96f / 255f));
        }
        if (type == TileType.Lung)
        {
            renderable.enabled = true;
            renderable.material.SetColor(
                "_Color", new Color(0.9f, 0.9f, 1.0f));
            T.localScale = new Vector3(1.0f, 1.0f, 1.0f) * 1.5f;
            audio_source = map.lungs_audio_source;
        }

        if (type == TileType.Alive)
        {
            renderable.enabled = true;
            Color color = new Color(
                    0.25f + 0.75f * Random.value,
                    0.25f + 0.75f * Random.value,
                    0.25f + 0.75f * Random.value);
            renderable.material.SetColor("_Color", 0.6f * color);
            T.localScale = new Vector3(1.0f, 1.0f, 1.0f) * 1.5f;
            blood_max = map.alive_max_blood;
            oxygen_max = map.alive_max_oxygen;
            blood_reserve = blood_max;
            oxygen_reserve = oxygen_max;

            child_tile = Instantiate(map.child_tile);
            Transform child_T = child_tile.GetComponent<Transform>();
            var child_R = child_tile.GetComponent<Renderer>();
            child_R.material.SetColor("_Color", color);
            child_T.localPosition = T.position + new Vector3(0f, 0f, -1f);
            child_T.localScale = T.localScale;
            child_tile_2 = Instantiate(map.child_tile);

            child_T = child_tile_2.GetComponent<Transform>();
            child_R = child_tile_2.GetComponent<Renderer>();
            child_R.material.SetColor("_Color", new Color(0.8f, 0f, 0f));
            child_T.localPosition = T.position + new Vector3(0f, 0f, 0.01f);
            child_T.localScale = new Vector3(1.0f, 1.0f, 0.01f);
        }
    }

    void TransferBlood(float blood_amount, float oxygen_amount)
    {
        uint connection_count = 0;
        float[] dpressure = new float[connection.Count];
        float normalization = 1e-5f;
        int i = 0;
        foreach(var tile in connection)
        {
            if (tile != null)
            {
                if (CanTransferBlood(tile) && tile.pressure < pressure)
                {
                    dpressure[i] = Mathf.Abs(pressure - tile.pressure);
                    normalization += dpressure[i];
                    connection_count++;
                }
            }
            i += 1;
        }

        if (connection_count == 0)
        {
            blood -= blood_amount;
            if (type == TileType.Vein || type == TileType.Alive)
                oxygen -= oxygen_amount;
        }

        i = 0;
        foreach (var tile in connection)
        {
            if (tile != null)
            {
                float fraction = dpressure[i] / normalization;
                float blood_flow_amount = fraction * blood_amount;
                float oxygen_flow_amount = fraction * oxygen_amount;
                if (CanTransferBlood(tile) && tile.pressure < pressure)
                {
                    tile.blood += blood_flow_amount;
                    tile.oxygen += oxygen_flow_amount;
                    if (CanTransferBlood(this))
                        blood -= blood_flow_amount;
                    if (type == TileType.Vein || type == TileType.Alive)
                        oxygen -= oxygen_flow_amount;
                }
                if (tile.type == TileType.Heart)
                {
                    blood -= blood_flow_amount;
                }
            }
            i += 1;
        }
    }

    public void SetVisualizationMode(VisualizationMode mode)
    {
        vis_mode = mode;
        SetTileColor();
    }

    void SetTileColor()
    {
        Renderer renderable = GetComponent<Renderer>();

        if (type == TileType.Vein)
        {
            switch (vis_mode)
            {
                case VisualizationMode.Blood:
                    renderable.material.SetColor(
                        "_Color", new Color(1.0f, 0.0f, 0.0f));
                    break;
                case VisualizationMode.Pressure:
                    renderable.material.SetColor(
                        "_Color", new Color(1.0f, 0.0f, 1.0f));
                    break;
                case VisualizationMode.Oxygen:
                    renderable.material.SetColor(
                        "_Color", new Color(0.95f, 0.95f, 1.0f));
                    break;
                default:
                    break;
            }
        }
    }

    bool CanTransferBlood(Tile tile)
    {
        if (tile.type == TileType.Vein || 
            tile.type == TileType.Lung || 
            tile.type == TileType.Alive)
            return true;
        return false;
    }

    public void KillCell()
    {
        is_dead_next = true;
        Renderer renderable = GetComponent<Renderer>();
        Color c = renderable.material.GetColor("_Color");
        c.r *= 0.5f;
        c.g *= 0.5f;
        c.b *= 0.5f;
        renderable.material.SetColor("_Color", c);

        if (child_tile != null)
        {
            renderable = child_tile.GetComponent<Renderer>();
            c = renderable.material.GetColor("_Color");
            c.r *= 0.5f;
            c.g *= 0.5f;
            c.b *= 0.5f;
            renderable.material.SetColor("_Color", c);
        }

        if (child_tile_2 != null)
        {
            renderable = child_tile_2.GetComponent<Renderer>();
            c = renderable.material.GetColor("_Color");
            c.r *= 0.5f;
            c.g *= 0.5f;
            c.b *= 0.5f;
            renderable.material.SetColor("_Color", c);
        }

    }
}
