using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using SoscMonoGame.Core;

namespace SoscMonoGame;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private Desktop? _desktop;
    private GameSession _gameSession = null!;

    private Label _lblPlayerHp = null!;
    private Label _lblPlayerGold = null!;
    private Label _lblMonsterInfo = null!;
    private VerticalStackPanel _logContainer = null!;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        _graphics.PreferredBackBufferWidth = 850;
        _graphics.PreferredBackBufferHeight = 600;
    }

    protected override void Initialize()
    {
        _gameSession = new GameSession();
        MyraEnvironment.Game = this;
        base.Initialize();
    }

    protected override void LoadContent()
    {
        var mainGrid = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 10,
            Padding = new Myra.Graphics2D.Thickness(15)
        };
        mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
        mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Fill));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));

        // 1. Left Card: Player Stats
        var playerCard = new Panel
        {
            Background = new SolidBrush(new Color(30, 30, 40)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        var pStack = new VerticalStackPanel { Spacing = 6 };
        pStack.Widgets.Add(new Label { Text = $"Player: {_gameSession.CurrentPlayer.Name}", TextColor = Color.Gold });
        _lblPlayerHp = new Label { TextColor = Color.LightGreen };
        _lblPlayerGold = new Label { TextColor = Color.Yellow };
        pStack.Widgets.Add(_lblPlayerHp);
        pStack.Widgets.Add(_lblPlayerGold);
        playerCard.Widgets.Add(pStack);
        Grid.SetColumn(playerCard, 0);
        Grid.SetRow(playerCard, 0);
        mainGrid.Widgets.Add(playerCard);

        // 2. Right Card: Current Monster
        var monsterCard = new Panel
        {
            Background = new SolidBrush(new Color(40, 25, 25)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        var mStack = new VerticalStackPanel { Spacing = 6 };
        mStack.Widgets.Add(new Label { Text = "=== Combat Encounter ===", TextColor = Color.IndianRed });
        _lblMonsterInfo = new Label { TextColor = Color.OrangeRed };
        mStack.Widgets.Add(_lblMonsterInfo);
        monsterCard.Widgets.Add(mStack);
        Grid.SetColumn(monsterCard, 1);
        Grid.SetRow(monsterCard, 0);
        mainGrid.Widgets.Add(monsterCard);

        // 3. Center Panel: Rolling Combat Log
        var logPanel = new Panel
        {
            Background = new SolidBrush(new Color(18, 18, 22)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        _logContainer = new VerticalStackPanel { Spacing = 4 };
        logPanel.Widgets.Add(_logContainer);
        Grid.SetColumn(logPanel, 0);
        Grid.SetColumnSpan(logPanel, 2);
        Grid.SetRow(logPanel, 1);
        mainGrid.Widgets.Add(logPanel);

        // 4. Bottom Row: Action Buttons
        var buttonBar = new HorizontalStackPanel { Spacing = 12 };
        var btnAttack = new Button { Content = new Label { Text = "Attack with Sword" } };
        btnAttack.Click += (s, a) =>
        {
            _gameSession.Attack();
            RefreshView();
        };

        var btnPotion = new Button { Content = new Label { Text = "Drink Potion" } };
        btnPotion.Click += (s, a) =>
        {
            _gameSession.UsePotion();
            RefreshView();
        };

        buttonBar.Widgets.Add(btnAttack);
        buttonBar.Widgets.Add(btnPotion);
        Grid.SetColumn(buttonBar, 0);
        Grid.SetColumnSpan(buttonBar, 2);
        Grid.SetRow(buttonBar, 2);
        mainGrid.Widgets.Add(buttonBar);

        _desktop = new Desktop { Root = mainGrid };
        RefreshView();
    }

    private void RefreshView()
    {
        var p = _gameSession.CurrentPlayer;
        _lblPlayerHp.Text = $"HP: {p.CurrentHitPoints} / {p.MaximumHitPoints}";
        _lblPlayerGold.Text = $"Gold: {p.Gold} G | XP: {p.ExperiencePoints}";

        var m = _gameSession.CurrentMonster;
        if (m != null)
        {
            _lblMonsterInfo.Text = $"Target: {m.Name}\nHP: {m.CurrentHitPoints} / {m.MaximumHitPoints}";
        }

        _logContainer.Widgets.Clear();
        foreach (var msg in _gameSession.CombatLog)
        {
            _logContainer.Widgets.Add(new Label { Text = msg, TextColor = Color.LightGray });
        }
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(15, 15, 18));
        _desktop?.Render();
        base.Draw(gameTime);
    }
}
