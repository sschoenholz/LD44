using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/* TODO LIST
 * 
 * NECESSARY:
 *  SOUND EFFECTS
 *  
 * NICE:
 *  HIGH SCORE BOARD
 *  GET EXTRA HEARTS / LUNGS LATEGAME 
 *      (NOT SURE IF THIS WILL MAKE GAME BETTER OR WORSE)
 *  ADD A WARNING IF YOU DIE BECAUSE OF OXYGENATION
 */

public enum State
{
    Playing,
    StartMenu,
    LoseMenu
}

enum EditMode
{
    None,
    Building,
    Deleting,
}

public class Map : MonoBehaviour
{
    [SerializeField]
    public GameObject tile;
    [SerializeField]
    public GameObject child_tile;
    [SerializeField]
    public int width;
    [SerializeField]
    public int height;

    [SerializeField]
    public Camera camera;

    [SerializeField]
    public float[] blood_prices;
    [SerializeField]
    public float heart_pump_blood;
    [SerializeField]
    public float heart_pump_pressure;

    [SerializeField]
    public float alive_max_blood;
    [SerializeField]
    public float alive_max_oxygen;
    [SerializeField]
    public float alive_blood_rate;
    [SerializeField]
    public float alive_oxygen_rate;

    [SerializeField]
    public float min_organ_time;
    [SerializeField]
    public float max_organ_time;

    [SerializeField]
    public int epoch_organs;

    [SerializeField]
    public GameObject visibility_labels;
    [SerializeField]
    public GameObject visibility_buttons;

    [SerializeField]
    public GameObject start_text;
    [SerializeField]
    public GameObject heart_text;
    [SerializeField]
    public GameObject lung_text;
    [SerializeField]
    public GameObject organ_text;

    [SerializeField]
    public GameObject score_text;

    [SerializeField]
    public GameObject died_text;

    [SerializeField]
    public float death_animation_time_per_frame;

    [SerializeField]
    public AudioClip make_vein_sound;
    [SerializeField]
    public AudioClip breath_sound;
    [SerializeField]
    public AudioClip heart_sound;
    [SerializeField]
    public AudioClip build_vein_sound;


    public State state;

    public AudioSource audio_source;
    public AudioSource lungs_audio_source;
    public AudioSource heart_audio_source;
    public AudioSource vein_audio_source;

    public float vein_sound_pitch;
    public float vein_last_build;

    private float game_start_time;
    private float game_time;

    private GameObject[,] tilemap;
    private Tile[,] tilemap_state;

    private Tile heart;
    private Tile lung;

    private float next_organ_time;

    private float last_death_animation_frame;

    // Start is called before the first frame update
    void Start()
    {
        audio_source = GetComponents<AudioSource>()[0];
        lungs_audio_source = GetComponents<AudioSource>()[1];
        lungs_audio_source.volume = 0.5f;
        heart_audio_source = GetComponents<AudioSource>()[2];
        heart_audio_source.volume = 0.25f;
        vein_audio_source = GetComponents<AudioSource>()[3];

        tilemap = new GameObject[width, height];
        tilemap_state = new Tile[width, height];

        var center = new Vector3(width / 2.0f, height / 2.0f + 2.0f, 0.0f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var pos = new Vector3(x, y, 0.0f) - center;
                tilemap[x, y] = Instantiate(tile, pos, new Quaternion());
                Transform T = tilemap[x, y].GetComponent<Transform>();
                T.SetParent(GetComponent<Transform>(), true);

                tilemap_state[x, y] = tilemap[x, y].GetComponent<Tile>();
                tilemap_state[x, y].map = this;
                tilemap_state[x, y].x = x;
                tilemap_state[x, y].y = y;
            }
        }

        StartMenu();
    }

    private bool is_deleting;
    private bool is_selecting;
    private EditMode edit_mode;

    private bool mouse_down_for_endgame;

    private float click_pitch;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Cancel"))
        {
            if (state == State.StartMenu)
            {
                Application.Quit();
                return;
            }
            state = State.StartMenu;
            StartMenu();
        }

        if (state == State.Playing)
        {
            if (Time.time - vein_last_build > 1.0f)
                vein_sound_pitch = 1.0f;

            if (Input.GetButtonDown("Select"))
            {
                click_pitch = 1.0f;
                is_selecting = true;
                edit_mode = EditMode.None;
            }

            if (Input.GetButtonUp("Select"))
            {
                is_selecting = false;
            }

            if (is_selecting)
            {
                RaycastHit hit;
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out hit))
                {
                    GameObject hit_object = hit.transform.gameObject;
                    Tile state = hit_object.GetComponent<Tile>();
                    if (state != null)
                    {
                        if (state.type == TileType.Flesh &&
                            (edit_mode == EditMode.Building || edit_mode == EditMode.None) &&
                            !state.is_building)
                        {
                            state.SetType(TileType.Vein);
                            state.is_building = true;
                            ResolveConnections();
                            edit_mode = EditMode.Building;

                            audio_source.pitch = click_pitch;
                            audio_source.PlayOneShot(make_vein_sound, 0.25f);
                            click_pitch += 0.015f;
                        }
                        else if (state.is_building &&
                          (edit_mode == EditMode.Deleting || edit_mode == EditMode.None))
                        {
                            state.SetType(TileType.Flesh);
                            state.is_building = false;
                            ResolveConnections();

                            if (edit_mode == EditMode.None)
                                click_pitch = 0.75f;
                            edit_mode = EditMode.Deleting;

                            audio_source.pitch = click_pitch;
                            audio_source.PlayOneShot(make_vein_sound, 0.25f);
                            click_pitch -= 0.015f;
                        }
                    }
                }
            }

            if (Input.GetButtonDown("Delete"))
            {
                click_pitch = 0.75f;
                is_deleting = true;
            }

            if (Input.GetButtonUp("Delete"))
                is_deleting = false;

            if (is_deleting)
            {
                RaycastHit hit;
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out hit))
                {
                    GameObject hit_object = hit.transform.gameObject;
                    Tile state = hit_object.GetComponentInParent<Tile>();
                    if (state != null)
                    {
                        if (state.type == TileType.Vein &&
                            !state.is_locked)
                        {
                            state.SetType(TileType.Flesh);
                            state.is_building = false;
                            ResolveConnections();

                            audio_source.pitch = click_pitch;
                            audio_source.PlayOneShot(make_vein_sound, 0.25f);
                            click_pitch -= 0.015f;
                        }
                    }
                }
            }

            if (Time.time > next_organ_time)
            {
                PlaceOrgan(TileType.Alive);
                next_organ_time = Time.time + Random.Range(
                    min_organ_time, max_organ_time);
            }

            float game_time = Time.time - game_start_time;
            int minutes = (int)Mathf.Floor(game_time / 60);
            string minutes_str = minutes.ToString("00");
            string seconds_str = (game_time % 60).ToString("00");

            var score = score_text.GetComponent<TextMeshProUGUI>();
            if(minutes > 0)
                score.text = string.Format(
                    "{0}m{1}s", minutes_str, seconds_str);
            else
                score.text = string.Format("   {0}s", seconds_str);
        }

        if (state == State.StartMenu)
        {
            if (Input.GetButtonUp("Select"))
            {
                StartGame();
            }
        }

        if (state == State.LoseMenu)
        {
            if (Input.GetButtonDown("Select"))
            {
                mouse_down_for_endgame = true;
            }
            if (Input.GetButtonUp("Select") && mouse_down_for_endgame)
            {
                mouse_down_for_endgame = false;
                StartMenu();
            }
            KillCells();
        }
    }

    void ResetConnections()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (tilemap_state[x, y].type == TileType.Vein || 
                    tilemap_state[x, y].type == TileType.Lung ||
                    tilemap_state[x, y].type == TileType.Alive)
                    for (int i = 0; i < 4; i++)
                    {
                        tilemap_state[x, y].connection.Clear();
                        tilemap_state[x, y].has_connections_established = false;
                        tilemap_state[x, y].pressure = 0.0f;
                    }
            }
        }
    }

    void ResolveConnections()
    {
        ResetConnections();

        Stack<Tile> need_to_visit = new Stack<Tile>();
        List<Tile> circuit = new List<Tile>();
        circuit.Add(heart);

        foreach (var adjacent in heart.connection)
        {
            if (adjacent.type == TileType.Vein)
                need_to_visit.Push(adjacent);
        }

        while (need_to_visit.Count > 0)
        {
            Tile current = need_to_visit.Pop();

            current.has_connections_established = true;
            circuit.Add(current);

            int cx = current.x;
            int cy = current.y;

            if (TilesCanConnect(current, tilemap_state[cx + 1, cy]))
            {
                tilemap_state[cx, cy].connection.Push(tilemap_state[cx + 1, cy]);
                if (!tilemap_state[cx + 1, cy].has_connections_established)
                    need_to_visit.Push(tilemap_state[cx + 1, cy]);
            }

            if (TilesCanConnect(current, tilemap_state[cx - 1, cy]))
            {
                tilemap_state[cx, cy].connection.Push(tilemap_state[cx - 1, cy]);
                if (!tilemap_state[cx - 1, cy].has_connections_established)
                    need_to_visit.Push(tilemap_state[cx - 1, cy]);
            }

            if (TilesCanConnect(current, tilemap_state[cx, cy + 1]))
            {
                tilemap_state[cx, cy].connection.Push(tilemap_state[cx, cy + 1]);
                if (!tilemap_state[cx, cy + 1].has_connections_established)
                    need_to_visit.Push(tilemap_state[cx, cy + 1]);
            }

            if (TilesCanConnect(current, tilemap_state[cx, cy - 1]))
            {
                tilemap_state[cx, cy].connection.Push(tilemap_state[cx, cy - 1]);
                if (!tilemap_state[cx, cy - 1].has_connections_established)
                    need_to_visit.Push(tilemap_state[cx, cy - 1]);
            }
        }

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int i = 0;
                tilemap_state[x, y].debug_connection = new Tile[
                    tilemap_state[x, y].connection.Count];
                foreach (var neighbor in tilemap_state[x, y].connection)
                {
                    tilemap_state[x, y].debug_connection[i] = neighbor;
                    i++;
                }
            }

        for (int i = 0; i < 1000; i++)
            AveragePressure(circuit);
    }

    void AveragePressure(List<Tile> circuit)
    {
        foreach (var tile in circuit)
        {
            if (tile.type == TileType.Vein || 
                tile.type == TileType.Lung || 
                tile.type == TileType.Alive)
            {
                int connection_count = tile.connection.Count;
                tile.pressure /= (connection_count + 1f);
                foreach (var neighbor in tile.connection)
                {
                    if (neighbor.type == TileType.Vein ||
                        (neighbor.type == TileType.Heart && neighbor.connection.Contains(tile)) ||
                        neighbor.type == TileType.Lung ||
                        neighbor.type == TileType.Alive)
                        tile.pressure += neighbor.pressure / (connection_count + 1f);
                }
            }
        }
    }

    public void SetVisualizationMode(VisualizationMode mode)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tilemap_state[x, y].SetVisualizationMode(mode);
            }
        }
    }

    bool TilesCanConnect(Tile a, Tile b)
    {
        if (a.type == TileType.Flesh || b.type == TileType.Flesh)
            return false;

        if (a.type == TileType.Vein &&
            (b.type == TileType.Vein || b.type == TileType.Heart || b.type == TileType.Lung || b.type == TileType.Alive))
            return true;

        if (b.type == TileType.Vein &&
            (a.type == TileType.Vein || a.type == TileType.Heart || a.type == TileType.Lung || a.type == TileType.Alive))
            return true;

        return false;
    }

    Tile PlaceOrgan(TileType organ) //int x, int y, TileType organ, TileConnection dir)
    {
        var x = Random.Range(4, width - 4);
        var y = Random.Range(4, height - 4);

        while(!CheckLocation(x, y))
        {
            x = Random.Range(4, width - 4);
            y = Random.Range(4, height - 4);
        }

        var dir = (TileConnection)Random.Range(0, 4);

        tilemap_state[x, y].SetType(organ);
        tilemap_state[x, y].has_connections_established = true;

        if (dir == TileConnection.Up)
        {
            tilemap_state[x, y + 1].SetType(TileType.Vein);
            tilemap_state[x, y + 1].is_locked = true;
            tilemap_state[x, y].connection.Push(tilemap_state[x, y + 1]);

            if (organ != TileType.Heart)
            {
                tilemap_state[x, y].connection.Push(tilemap_state[x, y - 1]);
                tilemap_state[x, y - 1].SetType(TileType.Vein);
                tilemap_state[x, y - 1].is_locked = true;
            }
        }

        if (dir == TileConnection.Down)
        {
            tilemap_state[x, y - 1].SetType(TileType.Vein);
            tilemap_state[x, y - 1].is_locked = true;
            tilemap_state[x, y].connection.Push(tilemap_state[x, y - 1]);

            if (organ != TileType.Heart)
            {
                tilemap_state[x, y].connection.Push(tilemap_state[x, y + 1]);
                tilemap_state[x, y + 1].SetType(TileType.Vein);
                tilemap_state[x, y + 1].is_locked = true;
            }
        }

        if (dir == TileConnection.Left)
        {
            tilemap_state[x - 1, y].SetType(TileType.Vein);
            tilemap_state[x - 1, y].is_locked = true;
            tilemap_state[x, y].connection.Push(tilemap_state[x - 1, y]);

            if (organ != TileType.Heart)
            {
                tilemap_state[x, y].connection.Push(tilemap_state[x + 1, y]);
                tilemap_state[x + 1, y].SetType(TileType.Vein);
                tilemap_state[x + 1, y].is_locked = true;
            }
        }

        if (dir == TileConnection.Right)
        {
            tilemap_state[x + 1, y].SetType(TileType.Vein);
            tilemap_state[x + 1, y].is_locked = true;
            tilemap_state[x, y].connection.Push(tilemap_state[x + 1, y]);

            if (organ != TileType.Heart)
            {
                tilemap_state[x, y].connection.Push(tilemap_state[x - 1, y]);
                tilemap_state[x - 1, y].SetType(TileType.Vein);
                tilemap_state[x - 1, y].is_locked = true;
            }
        }

        return tilemap_state[x, y];
    }

    bool CheckLocation(int x, int y)
    {
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                if (tilemap_state[x + dx, y + dy].type != TileType.Flesh)
                    return false;
            }
        }

        return true;
    }

    public void StartMenu()
    {
        RestartGame();
        state = State.StartMenu;
        visibility_buttons.SetActive(false);
        visibility_labels.SetActive(false);
        start_text.SetActive(true);
        heart_text.SetActive(true);
        lung_text.SetActive(true);
        organ_text.SetActive(true);
        died_text.SetActive(false);
    }

    public void StartGame()
    {
        state = State.Playing;
        next_organ_time = Time.time + 15.0f;
        game_start_time = Time.time;
        visibility_buttons.SetActive(true);
        visibility_labels.SetActive(true);
        start_text.SetActive(false);
        heart_text.SetActive(false);
        organ_text.SetActive(false);
        lung_text.SetActive(false);
    }

    public void LoseGame(Tile tile)
    {
        visibility_buttons.SetActive(false);
        visibility_labels.SetActive(false);
        died_text.SetActive(true);
        state = State.LoseMenu;

        game_time = Time.time - game_start_time;
        int minutes = (int)Mathf.Floor(game_time / 60);
        string minutes_str = minutes.ToString();
        string seconds_str = ((int) (game_time % 60)).ToString();

        var score = died_text.GetComponent<TextMeshProUGUI>();
        if (minutes > 0)
            score.text = string.Format("You died after {0}m {1}s",
                minutes_str, seconds_str);
        else
            score.text = string.Format("You died after {0}s", seconds_str);

        tile.KillCell();

        Vector3 organ_pos = tilemap[tile.x, tile.y].GetComponent<Transform>().localPosition;
        organ_pos += new Vector3(10.25f, -2.6f, -1.0f);
    }

    public void RestartGame()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tilemap_state[x, y].SetType(TileType.Flesh);
            }
        }

        heart = PlaceOrgan(TileType.Heart);
        Vector3 heart_pos = tilemap[heart.x, heart.y].GetComponent<Transform>().localPosition;
        heart_pos += new Vector3(11.0f, 0.0f, -1.0f);
        heart_text.GetComponent<Transform>().localPosition = heart_pos;

        lung = PlaceOrgan(TileType.Lung);
        Vector3 lung_pos = tilemap[lung.x, lung.y].GetComponent<Transform>().localPosition;
        lung_pos += new Vector3(11.0f, 0.0f, -1.0f);
        lung_text.GetComponent<Transform>().localPosition = lung_pos;

        var organ = PlaceOrgan(TileType.Alive);
        tilemap[organ.x, organ.y].GetComponent<Renderer>().material.SetColor(
            "_Color", 0.6f * organ_text.GetComponent<TextMeshPro>().color);
        tilemap_state[organ.x, organ.y].child_tile.GetComponent<Renderer>().material.SetColor(
            "_Color", organ_text.GetComponent<TextMeshPro>().color);
        Vector3 organ_pos = tilemap[organ.x, organ.y].GetComponent<Transform>().localPosition;
        organ_pos += new Vector3(11.0f, 0.0f, -1.0f);
        organ_text.GetComponent<Transform>().localPosition = organ_pos;

        next_organ_time = Time.time + 15.0f;

        ResolveConnections();
    }

    void KillCells()
    {
        if (Time.time - last_death_animation_frame < death_animation_time_per_frame)
            return;
        last_death_animation_frame = Time.time;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (tilemap_state[x, y].is_dead || tilemap_state[x, y].is_dead_next)
                    continue;
                if (x > 0 && tilemap_state[x - 1, y].is_dead)
                    tilemap_state[x, y].KillCell();
                else if (x < width - 1 && tilemap_state[x + 1, y].is_dead)
                    tilemap_state[x, y].KillCell();
                else if (y > 0 && tilemap_state[x, y - 1].is_dead)
                    tilemap_state[x, y].KillCell();
                else if (y < height - 1 && tilemap_state[x, y + 1].is_dead)
                    tilemap_state[x, y].KillCell();
            }
        }
    }
}
