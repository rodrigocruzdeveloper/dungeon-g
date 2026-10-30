using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


// ============================================================================
// DUNGEON GENERATOR 2D
// ============================================================================
//
// Este script cria automaticamente um ambiente 2D utilizando Tilemaps.
//
// A ideia básica do sistema é:
//
// 1. Criar várias salas em posições aleatórias.
// 2. Verificar se as salas não estão sobrepostas.
// 3. Conectar as salas através de corredores.
// 4. Transformar as salas e corredores em tiles de chão.
// 5. Procurar as bordas do chão.
// 6. Colocar tiles de parede nessas bordas.
//
// O resultado é um pequeno dungeon gerado proceduralmente.
//
// IMPORTANTE:
// Este exemplo trabalha primeiro com DADOS (posições e salas) e somente
// depois transforma esses dados em elementos visuais no Tilemap.
//
// Isso é uma ideia importante em geração procedural:
//
//      DADOS → REGRAS → REPRESENTAÇÃO VISUAL
//
// ============================================================================


public class Level : MonoBehaviour
{
    // =========================================================================
    // TILEMAPS
    // =========================================================================
    //
    // O Tilemap é o objeto responsável por armazenar os tiles na cena.
    //
    // Teremos dois Tilemaps:
    //
    // Floor = onde serão colocados os tiles de chão.
    // Walls = onde serão colocados os tiles de parede.
    //
    // [SerializeField] permite que esses campos apareçam no Inspector mesmo
    // estando declarados como private.
    // =========================================================================

    [Header("Tilemaps")]

    [SerializeField]
    private Tilemap floorTilemap;

    [SerializeField]
    private Tilemap wallTilemap;


    // =========================================================================
    // TILES
    // =========================================================================
    //
    // Aqui informamos quais imagens/tile assets serão utilizados.
    //
    // floorTile = tile utilizado para representar o chão.
    // wallTile  = tile utilizado para representar a parede.
    //
    // Esses objetos serão associados pelo Inspector do Unity.
    // =========================================================================

    [Header("Tiles")]

    [SerializeField]
    private TileBase floorTile;

    [SerializeField]
    private TileBase wallTile;


    // =========================================================================
    // CONFIGURAÇÃO DAS SALAS
    // =========================================================================
    //
    // Esses valores controlam o tamanho e a quantidade das salas.
    //
    // Por exemplo:
    //
    // numberOfRooms = 15
    //
    // significa que o algoritmo tentará criar até 15 salas.
    //
    // minRoomWidth = 6
    // maxRoomWidth = 12
    //
    // significa que uma sala poderá ter entre 6 e 12 tiles de largura.
    //
    // O mesmo conceito vale para a altura.
    // =========================================================================

    [Header("Dungeon")]

    [SerializeField]
    private int numberOfRooms = 15;

    [SerializeField]
    private int minRoomWidth = 6;

    [SerializeField]
    private int maxRoomWidth = 12;

    [SerializeField]
    private int minRoomHeight = 6;

    [SerializeField]
    private int maxRoomHeight = 10;


    // =========================================================================
    // DISTÂNCIA ENTRE SALAS
    // =========================================================================
    //
    // Esse valor é utilizado para evitar que duas salas sejam criadas muito
    // próximas umas das outras.
    //
    // Por exemplo:
    //
    // roomSpacing = 3
    //
    // significa que, antes de aceitar uma nova sala, vamos criar
    // temporariamente uma área maior ao redor das salas existentes.
    //
    // Se a nova sala entrar nessa área, consideramos que existe uma
    // sobreposição ou proximidade excessiva.
    // =========================================================================

    [Header("Distância entre salas")]

    [SerializeField]
    private int roomSpacing = 3;


    // =========================================================================
    // SEED
    // =========================================================================
    //
    // A seed é um número utilizado para controlar a aleatoriedade.
    //
    // Se utilizarmos sempre a mesma seed, teremos sempre o mesmo dungeon.
    //
    // Isso é muito importante em jogos procedurais.
    //
    // Exemplo:
    //
    // Seed 12345 → Dungeon A
    // Seed 12345 → Dungeon A novamente
    //
    // Seed 98765 → Dungeon B
    //
    // Assim podemos reproduzir uma geração específica.
    // =========================================================================

    [Header("Seed")]

    [SerializeField]
    private bool randomSeed = true;

    [SerializeField]
    private int seed = 12345;


    // =========================================================================
    // GERADOR DE NÚMEROS ALEATÓRIOS
    // =========================================================================
    //
    // System.Random é utilizado para gerar números aleatórios.
    //
    // Estamos utilizando System.Random em vez de Random do Unity porque
    // queremos controlar a seed manualmente.
    //
    // Dessa forma conseguimos reproduzir exatamente a mesma sequência
    // de números aleatórios quando utilizamos a mesma seed.
    // =========================================================================

    private System.Random random;


    // =========================================================================
    // LISTA DE SALAS
    // =========================================================================
    //
    // Cada sala será representada por um RectInt.
    //
    // RectInt é uma estrutura do Unity que representa um retângulo utilizando
    // coordenadas inteiras.
    //
    // Uma sala pode ser representada, por exemplo, por:
    //
    // posição X = 10
    // posição Y = 5
    // largura = 8
    // altura = 6
    //
    // Ou seja:
    //
    // RectInt(10, 5, 8, 6)
    //
    // Essa informação ainda não é visual.
    //
    // É apenas a descrição matemática da sala.
    // =========================================================================

    private List<RectInt> rooms =
        new List<RectInt>();


    // =========================================================================
    // POSIÇÕES QUE SERÃO CHÃO
    // =========================================================================
    //
    // Aqui guardamos todas as posições que deverão receber um tile de chão.
    //
    // HashSet é utilizado porque não queremos armazenar a mesma posição
    // várias vezes.
    //
    // Por exemplo:
    //
    // (5,5)
    // (6,5)
    // (7,5)
    //
    // Cada posição representa um tile.
    //
    // Como os corredores podem passar por posições que já pertencem a uma
    // sala, o HashSet é especialmente útil.
    // =========================================================================

    private HashSet<Vector2Int> floorPositions =
        new HashSet<Vector2Int>();


    // =========================================================================
    // START
    // =========================================================================
    //
    // O Unity executa Start() automaticamente quando o objeto entra em jogo.
    //
    // Aqui simplesmente iniciamos o processo de geração.
    // =========================================================================

    private void Start()
    {
        GenerateDungeon();
    }


    // =========================================================================
    // GENERATE DUNGEON
    // =========================================================================
    //
    // Este é o método principal do sistema.
    //
    // Ele organiza todo o processo de geração.
    //
    // A ordem é importante:
    //
    // 1. Limpar o dungeon anterior.
    // 2. Configurar a seed.
    // 3. Criar as salas.
    // 4. Criar os corredores.
    // 5. Criar o chão.
    // 6. Criar as paredes.
    //
    // Podemos pensar nesse método como o "diretor" da geração.
    // =========================================================================

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        // Primeiro removemos qualquer dungeon que tenha sido criado
        // anteriormente.

        ClearDungeon();


        // Se randomSeed estiver ativado, criamos uma seed aleatória.
        //
        // Random.Range gera um número dentro do intervalo informado.

        if (randomSeed)
        {
            seed = Random.Range(
                int.MinValue,
                int.MaxValue
            );
        }


        // Criamos o gerador System.Random utilizando a seed escolhida.

        random = new System.Random(seed);


        // Primeiro criamos as salas.

        GenerateRooms();


        // Depois conectamos essas salas.

        GenerateCorridors();


        // Transformamos as salas e corredores em posições de chão.

        GenerateFloor();


        // Finalmente criamos as paredes ao redor do chão.

        GenerateWalls();
    }


    // =========================================================================
    // GENERATE ROOMS
    // =========================================================================
    //
    // Este método é responsável por criar as salas.
    //
    // Ele não cria GameObjects.
    //
    // Ele apenas calcula onde as salas deverão existir.
    //
    // Essa separação é importante:
    //
    // PRIMEIRO:
    // decidimos onde está o dungeon.
    //
    // DEPOIS:
    // desenhamos esse dungeon.
    // =========================================================================

    private void GenerateRooms()
    {
        // Repetimos o processo até atingir a quantidade desejada
        // de tentativas.

        for (int i = 0; i < numberOfRooms; i++)
        {

            // Sorteamos a largura da sala.
            //
            // O segundo parâmetro de Next é exclusivo.
            // Por isso usamos maxRoomWidth + 1.

            int width = random.Next(
                minRoomWidth,
                maxRoomWidth + 1
            );


            // Fazemos exatamente a mesma coisa para a altura.

            int height = random.Next(
                minRoomHeight,
                maxRoomHeight + 1
            );


            // Agora sorteamos a posição da sala.
            //
            // O dungeon poderá se espalhar para coordenadas positivas
            // e negativas.

            int x = random.Next(-50, 51);

            int y = random.Next(-30, 31);


            // Criamos um RectInt representando a nova sala.

            RectInt newRoom =
                new RectInt(
                    x,
                    y,
                    width,
                    height
                );


            // Começamos assumindo que não existe sobreposição.

            bool overlaps = false;


            // Agora precisamos comparar a nova sala com todas as salas
            // que já foram criadas.

            foreach (RectInt room in rooms)
            {

                // Criamos temporariamente um retângulo maior ao redor
                // da sala existente.
                //
                // Isso cria uma margem de segurança entre as salas.

                RectInt expandedRoom =
                    new RectInt(
                        room.xMin - roomSpacing,
                        room.yMin - roomSpacing,
                        room.width + roomSpacing * 2,
                        room.height + roomSpacing * 2
                    );


                // Overlaps verifica se os dois retângulos se sobrepõem.
                //
                // Se isso acontecer, a nova sala não será aceita.

                if (expandedRoom.Overlaps(newRoom))
                {
                    overlaps = true;

                    break;
                }
            }


            // Se a sala não entrou em conflito com nenhuma outra,
            // podemos adicioná-la à lista.

            if (!overlaps)
            {
                rooms.Add(newRoom);
            }
        }
    }


    // =========================================================================
    // GENERATE CORRIDORS
    // =========================================================================
    //
    // Depois de criar as salas, precisamos conectá-las.
    //
    // Para cada sala, pegamos o centro dela e conectamos ao centro da
    // sala anterior.
    //
    // Isso cria uma estrutura simples de dungeon.
    // =========================================================================

    private void GenerateCorridors()
    {
        // Começamos na segunda sala porque a primeira não possui
        // uma sala anterior.

        for (int i = 1; i < rooms.Count; i++)
        {

            // Pegamos o centro da sala anterior.

            Vector2Int previousCenter =
                GetRoomCenter(rooms[i - 1]);


            // Pegamos o centro da sala atual.

            Vector2Int currentCenter =
                GetRoomCenter(rooms[i]);


            // Sorteamos se o corredor começará na horizontal
            // ou na vertical.
            //
            // Isso cria pequenas variações na aparência dos corredores.

            if (random.Next(0, 2) == 0)
            {

                // Primeiro caminhamos na horizontal.

                CreateHorizontalCorridor(
                    previousCenter.x,
                    currentCenter.x,
                    previousCenter.y
                );


                // Depois caminhamos na vertical.

                CreateVerticalCorridor(
                    previousCenter.y,
                    currentCenter.y,
                    currentCenter.x
                );
            }
            else
            {

                // Neste caso fazemos o contrário:
                // primeiro vertical.

                CreateVerticalCorridor(
                    previousCenter.y,
                    currentCenter.y,
                    previousCenter.x
                );


                // Depois horizontal.

                CreateHorizontalCorridor(
                    previousCenter.x,
                    currentCenter.x,
                    currentCenter.y
                );
            }
        }
    }


    // =========================================================================
    // CREATE HORIZONTAL CORRIDOR
    // =========================================================================
    //
    // Cria um corredor horizontal entre duas posições X.
    //
    // Exemplo:
    //
    // startX = 2
    // endX   = 8
    //
    // O método criará:
    //
    // 2 3 4 5 6 7 8
    //
    // Além disso, utilizamos duas linhas de altura para deixar o corredor
    // com 2 tiles de largura.
    // =========================================================================

    private void CreateHorizontalCorridor(
        int startX,
        int endX,
        int y)
    {

        // Mathf.Min retorna o menor valor.
        //
        // Assim podemos trabalhar tanto da esquerda para a direita
        // quanto da direita para a esquerda.

        int min = Mathf.Min(startX, endX);


        // Mathf.Max retorna o maior valor.

        int max = Mathf.Max(startX, endX);


        // Percorremos todas as posições entre os dois pontos.

        for (int x = min; x <= max; x++)
        {

            // Adicionamos o primeiro tile do corredor.

            floorPositions.Add(
                new Vector2Int(x, y)
            );


            // Adicionamos o segundo tile.
            //
            // Isso deixa o corredor com 2 tiles de altura.

            floorPositions.Add(
                new Vector2Int(x, y + 1)
            );
        }
    }


    // =========================================================================
    // CREATE VERTICAL CORRIDOR
    // =========================================================================
    //
    // Faz a mesma coisa que o método anterior, mas no eixo Y.
    //
    // Exemplo:
    //
    // startY = 2
    // endY   = 8
    //
    // O método criará:
    //
    // 2
    // 3
    // 4
    // 5
    // 6
    // 7
    // 8
    // =========================================================================

    private void CreateVerticalCorridor(
        int startY,
        int endY,
        int x)
    {

        int min = Mathf.Min(startY, endY);

        int max = Mathf.Max(startY, endY);


        for (int y = min; y <= max; y++)
        {

            // Primeiro tile do corredor.

            floorPositions.Add(
                new Vector2Int(x, y)
            );


            // Segundo tile.
            //
            // Isso deixa o corredor com 2 tiles de largura.

            floorPositions.Add(
                new Vector2Int(x + 1, y)
            );
        }
    }


    // =========================================================================
    // GENERATE FLOOR
    // =========================================================================
    //
    // Agora temos as salas e corredores definidos matematicamente.
    //
    // Precisamos transformar essas posições em elementos visuais.
    //
    // Primeiro adicionamos ao HashSet todas as posições que pertencem
    // às salas.
    // =========================================================================

    private void GenerateFloor()
    {

        // Percorremos todas as salas criadas.

        foreach (RectInt room in rooms)
        {

            // Percorremos cada posição X dentro da sala.

            for (int x = room.xMin; x < room.xMax; x++)
            {

                // Percorremos cada posição Y dentro da sala.

                for (int y = room.yMin; y < room.yMax; y++)
                {

                    // Guardamos a posição como uma posição de chão.

                    floorPositions.Add(
                        new Vector2Int(x, y)
                    );
                }
            }
        }


        // Agora percorremos todas as posições de chão.

        foreach (Vector2Int position in floorPositions)
        {

            // SetTile coloca um Tile no Tilemap.
            //
            // Vector2Int precisa ser convertido para Vector3Int porque
            // o Tilemap trabalha com coordenadas de três dimensões.

            floorTilemap.SetTile(
                (Vector3Int)position,
                floorTile
            );
        }
    }


    // =========================================================================
    // GENERATE WALLS
    // =========================================================================
    //
    // Agora vem uma das partes mais interessantes do algoritmo.
    //
    // Nós não precisamos descobrir manualmente onde ficam as paredes.
    //
    // Podemos descobrir isso analisando os tiles de chão.
    //
    // Para cada tile de chão:
    //
    //     ↑
    //     ↓
    //     ← →
    //
    // verificamos se existe chão ao lado.
    //
    // Se NÃO existir chão, colocamos uma parede naquela posição.
    // =========================================================================

    private void GenerateWalls()
    {

        // Criamos as quatro direções possíveis.

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };


        // Percorremos todos os tiles de chão.

        foreach (Vector2Int floorPosition in floorPositions)
        {

            // Para cada tile de chão, verificamos as quatro direções.

            foreach (Vector2Int direction in directions)
            {

                // Calculamos a posição que estamos verificando.

                Vector2Int wallPosition =
                    floorPosition + direction;


                // Se não existe chão nessa posição,
                // significa que estamos na borda do dungeon.

                if (!floorPositions.Contains(wallPosition))
                {

                    // Colocamos um tile de parede nessa posição.

                    wallTilemap.SetTile(
                        (Vector3Int)wallPosition,
                        wallTile
                    );
                }
            }
        }
    }


    // =========================================================================
    // GET ROOM CENTER
    // =========================================================================
    //
    // Retorna o centro de uma sala.
    //
    // Precisamos do centro porque os corredores serão criados
    // conectando os centros das salas.
    // =========================================================================

    private Vector2Int GetRoomCenter(RectInt room)
    {

        return new Vector2Int(
            room.xMin + room.width / 2,
            room.yMin + room.height / 2
        );
    }


    // =========================================================================
    // CLEAR DUNGEON
    // =========================================================================
    //
    // Antes de criar um novo dungeon, precisamos apagar o anterior.
    //
    // Caso contrário, os novos tiles seriam adicionados sobre os antigos.
    // =========================================================================

    private void ClearDungeon()
    {

        // Remove todas as posições armazenadas.

        floorPositions.Clear();


        // Remove todas as salas armazenadas.

        rooms.Clear();


        // Apaga os tiles do Tilemap de chão.

        floorTilemap.ClearAllTiles();


        // Apaga os tiles do Tilemap de paredes.

        wallTilemap.ClearAllTiles();
    }
}